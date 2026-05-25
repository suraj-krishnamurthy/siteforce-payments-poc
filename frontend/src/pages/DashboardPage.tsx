import { useEffect, useState, useRef, useCallback } from 'react'
import api from '../api/client'

const RUPEE = '\u20B9'

interface PaymentLine {
  id: number
  paymentRunId: number
  workerId: string
  siteName: string
  grossAmount: number
  deductions: number
  allowances: number
  netAmount: number
  status: string
  breakdownJson: string
}

interface PaginatedResponse {
  items: PaymentLine[]
  totalCount: number
  page: number
  pageSize: number
  hasMore: boolean
}

interface Batch {
  id: number
  siteName: string
  period: string
  status: string
  totalAmount: number
  createdAt: string
  approvedBy: string | null
  approvedAt: string | null
  workerCount: number
}

export default function DashboardPage() {
  const [payments, setPayments] = useState<PaymentLine[]>([])
  const [allPayments, setAllPayments] = useState<PaymentLine[]>([])
  const [batches, setBatches] = useState<Batch[]>([])
  const [sites, setSites] = useState<string[]>([])
  const [filterSite, setFilterSite] = useState('')
  const [filterStatus, setFilterStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [page, setPage] = useState(1)
  const [hasMore, setHasMore] = useState(false)
  const [totalCount, setTotalCount] = useState(0)

  const observerRef = useRef<IntersectionObserver | null>(null)
  const pageRef = useRef(1)
  const hasMoreRef = useRef(false)
  const loadingMoreRef = useRef(false)
  const filterSiteRef = useRef('')
  const filterStatusRef = useRef('')

  // Keep refs in sync
  pageRef.current = page
  hasMoreRef.current = hasMore
  loadingMoreRef.current = loadingMore
  filterSiteRef.current = filterSite
  filterStatusRef.current = filterStatus

  const lastRowRef = useCallback((node: HTMLTableRowElement | null) => {
    if (observerRef.current) observerRef.current.disconnect()
    observerRef.current = new IntersectionObserver((entries) => {
      if (entries[0].isIntersecting && hasMoreRef.current && !loadingMoreRef.current) {
        loadMore()
      }
    })
    if (node) observerRef.current.observe(node)
  }, [])

  const fetchSites = async () => {
    try {
      const res = await api.get<PaginatedResponse>('/payments', { params: { pageSize: 10000 } })
      const allSites = [...new Set(res.data.items.map((p) => p.siteName))]
      setSites(allSites)
      setAllPayments(res.data.items)
    } catch {
      // silently fail
    }
  }

  const fetchData = async (resetPage = true) => {
    setLoading(true)
    const currentPage = resetPage ? 1 : page
    if (resetPage) setPage(1)

    try {
      const [paymentsRes, batchesRes, allRes] = await Promise.all([
        api.get<PaginatedResponse>('/payments', {
          params: {
            page: currentPage,
            pageSize: 50,
            ...(filterSite && { siteName: filterSite }),
            ...(filterStatus && { status: filterStatus }),
          },
        }),
        api.get<Batch[]>('/batches'),
        api.get<PaginatedResponse>('/payments', { params: { pageSize: 10000 } }),
      ])
      setPayments(paymentsRes.data.items)
      setHasMore(paymentsRes.data.hasMore)
      setTotalCount(paymentsRes.data.totalCount)
      setBatches(batchesRes.data)
      setAllPayments(allRes.data.items)
    } catch {
      // silently fail for POC
    } finally {
      setLoading(false)
    }
  }

  const loadMore = async () => {
    if (loadingMore) return
    const nextPage = pageRef.current + 1
    setLoadingMore(true)
    try {
      const res = await api.get<PaginatedResponse>('/payments', {
        params: {
          page: nextPage,
          pageSize: 50,
          ...(filterSiteRef.current && { siteName: filterSiteRef.current }),
          ...(filterStatusRef.current && { status: filterStatusRef.current }),
        },
      })
      setPayments((prev) => [...prev, ...res.data.items])
      setHasMore(res.data.hasMore)
      setTotalCount(res.data.totalCount)
      setPage(nextPage)
    } catch {
      // ignore
    } finally {
      setLoadingMore(false)
    }
  }

  const handleApprove = async (batchId: number) => {
    try {
      await api.post(`/batches/${batchId}/approve`)
      fetchData(true)
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Approval failed')
    }
  }

  useEffect(() => {
    fetchSites()
  }, [])

  useEffect(() => {
    fetchData(true)
  }, [filterSite, filterStatus])

  const statusBadge = (status: string) => {
    switch (status) {
      case 'Ready':
      case 'Calculated':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-500/10 text-emerald-400 ring-1 ring-emerald-500/20">{status}</span>
      case 'Disputed':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-rose-500/10 text-rose-400 ring-1 ring-rose-500/20">Disputed</span>
      case 'Pending':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-500/10 text-amber-400 ring-1 ring-amber-500/20">Pending</span>
      case 'Approved':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-sky-500/10 text-sky-400 ring-1 ring-sky-500/20">Approved</span>
      default:
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold bg-slate-500/10 text-slate-400 ring-1 ring-slate-500/20">{status}</span>
    }
  }

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <div className="flex flex-col items-center gap-3">
        <div className="w-7 h-7 border-2 border-slate-700 border-t-indigo-400 rounded-full animate-spin"></div>
        <p className="text-sm text-slate-500">Loading dashboard...</p>
      </div>
    </div>
  )

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)]">
      <div className="mb-6">
        <h2 className="text-2xl font-bold text-slate-100">Payment Dashboard</h2>
        <p className="text-sm text-slate-500 mt-1">Manage and approve site payment batches</p>
      </div>

      {/* Batches */}
      {batches.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6 gap-3 mb-6">
          {batches.map((batch) => {
            const batchPayments = allPayments.filter((p) => p.paymentRunId === batch.id)
            const disputedCount = batchPayments.filter((p) => p.status === 'Disputed').length
            const hasDisputes = disputedCount > 0
            const readyCount = batchPayments.filter((p) => p.status === 'Ready').length

            return (
            <div key={batch.id} className="bg-slate-900/80 border border-slate-800/80 rounded-xl p-3.5 hover:border-slate-700/80 transition-all duration-200 flex flex-col h-[160px]">
              {/* Top: site name + chips */}
              <div className="flex justify-between items-start">
                <h3 className="font-semibold text-sm text-slate-200 leading-tight truncate mr-2">{batch.siteName}</h3>
                <div className="flex flex-col items-end gap-1 flex-shrink-0">
                  {readyCount > 0 && (
                    <span className="relative group/ready px-1.5 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 ring-1 ring-emerald-500/20 cursor-default">
                      {readyCount}
                      <span className="hidden group-hover/ready:block absolute bottom-full right-0 mb-1.5 whitespace-nowrap px-2 py-1 bg-slate-800 text-slate-200 text-[10px] rounded shadow-lg border border-slate-700 z-10">
                        {readyCount} workers ready for payment
                      </span>
                    </span>
                  )}
                  {disputedCount > 0 && (
                    <span className="relative group/disp px-1.5 py-0.5 rounded text-[10px] font-semibold bg-rose-500/10 text-rose-400 ring-1 ring-rose-500/20 cursor-default">
                      {disputedCount}
                      <span className="hidden group-hover/disp:block absolute bottom-full right-0 mb-1.5 whitespace-nowrap px-2 py-1 bg-slate-800 text-slate-200 text-[10px] rounded shadow-lg border border-slate-700 z-10">
                        {disputedCount} workers below dispute threshold
                      </span>
                    </span>
                  )}
                </div>
              </div>

              {/* Middle: amount + meta */}
              <div className="mt-2 flex-1">
                <p className="text-xl font-bold text-indigo-400 leading-none">{RUPEE}{(batch.totalAmount / 100000).toFixed(1)}<span className="text-sm font-medium text-indigo-400/60 ml-0.5">L</span></p>
                <p className="text-[11px] text-slate-500 mt-1">{batch.workerCount} workers &middot; {batch.period}</p>
              </div>

              {/* Bottom: action - always at bottom */}
              <div className="mt-auto pt-2">
                {batch.status !== 'Approved' && !hasDisputes && (
                  <button
                    onClick={() => handleApprove(batch.id)}
                    className="w-full px-2.5 py-1.5 bg-indigo-600/80 text-white text-[11px] font-medium rounded-lg hover:bg-indigo-500 active:scale-[0.97] transition-all"
                  >
                    Approve Batch
                  </button>
                )}
                {batch.status !== 'Approved' && hasDisputes && (
                  <div className="relative group/tip">
                    <button
                      disabled
                      className="w-full px-2.5 py-1.5 bg-slate-800/80 text-slate-500 text-[11px] font-medium rounded-lg cursor-not-allowed border border-slate-700/50"
                    >
                      Approve Batch
                    </button>
                    <div className="hidden group-hover/tip:block absolute bottom-full left-1/2 -translate-x-1/2 mb-2 w-52 p-2.5 bg-slate-800 text-slate-200 text-[11px] rounded-lg shadow-xl border border-slate-700 z-10">
                      <p className="font-medium mb-0.5 text-rose-400">{disputedCount} disputed</p>
                      <p className="text-slate-400">Workers below threshold need resolution before batch approval.</p>
                      <div className="absolute top-full left-1/2 -translate-x-1/2 border-4 border-transparent border-t-slate-800"></div>
                    </div>
                  </div>
                )}
                {batch.status === 'Approved' && (
                  <div className="flex items-center gap-1 text-[10px] text-emerald-400/80">
                    <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 20 20"><path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z" clipRule="evenodd"/></svg>
                    <span>{batch.approvedBy}</span>
                  </div>
                )}
              </div>
            </div>
            )
          })}
        </div>
      )}

      {/* Filters */}
      <div className="flex flex-wrap gap-3 mb-4 items-center bg-slate-900/60 border border-slate-800/60 rounded-lg px-4 py-3">
        <select
          value={filterSite}
          onChange={(e) => setFilterSite(e.target.value)}
          className="border border-slate-700/60 rounded-md px-3 py-2 text-sm bg-slate-800/80 text-slate-300 focus:outline-none focus:ring-1 focus:ring-indigo-500/40 focus:border-indigo-500/60"
        >
          <option value="">All Sites</option>
          {sites.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <select
          value={filterStatus}
          onChange={(e) => setFilterStatus(e.target.value)}
          className="border border-slate-700/60 rounded-md px-3 py-2 text-sm bg-slate-800/80 text-slate-300 focus:outline-none focus:ring-1 focus:ring-indigo-500/40 focus:border-indigo-500/60"
        >
          <option value="">All Statuses</option>
          <option value="Ready">Ready</option>
          <option value="Disputed">Disputed</option>
          <option value="Pending">Pending</option>
        </select>
        <div className="ml-auto flex items-center gap-1.5">
          <div className="w-1.5 h-1.5 bg-indigo-400 rounded-full animate-pulse"></div>
          <span className="text-xs text-slate-500">
            {Math.min(payments.length, totalCount)} of {totalCount}
          </span>
        </div>
      </div>

      {/* Payment Lines Table */}
      {payments.length === 0 ? (
        <div className="bg-slate-900/60 border border-slate-800/60 rounded-xl p-10 text-center flex-1">
          <p className="text-slate-500 text-sm">No payment data. Upload attendance and calculate first.</p>
        </div>
      ) : (
        <div className="bg-slate-900/60 border border-slate-800/60 rounded-xl overflow-hidden flex-1 flex flex-col min-h-0">
          <div className="overflow-auto flex-1">
            <table className="min-w-full">
              <thead className="bg-[#13151d] sticky top-0 border-b border-slate-700/50 z-10">
                <tr>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-400 uppercase tracking-wider">Worker ID</th>
                  <th className="px-4 py-3 text-left text-xs font-semibold text-slate-400 uppercase tracking-wider">Site</th>
                  <th className="px-4 py-3 text-right text-xs font-semibold text-slate-400 uppercase tracking-wider">Gross</th>
                  <th className="px-4 py-3 text-right text-xs font-semibold text-slate-400 uppercase tracking-wider">Recovery</th>
                  <th className="px-4 py-3 text-right text-xs font-semibold text-slate-400 uppercase tracking-wider">Allowance</th>
                  <th className="px-4 py-3 text-right text-xs font-semibold text-slate-400 uppercase tracking-wider">Net Pay</th>
                  <th className="px-4 py-3 text-center text-xs font-semibold text-slate-400 uppercase tracking-wider">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/30">
                {payments.map((p, index) => (
                  <tr
                    key={p.id}
                    ref={index === payments.length - 1 ? lastRowRef : null}
                    className="hover:bg-indigo-500/[0.04] transition-colors"
                  >
                    <td className="px-4 py-2.5 text-sm font-mono text-slate-300">{p.workerId}</td>
                    <td className="px-4 py-2.5 text-sm text-slate-400">{p.siteName}</td>
                    <td className="px-4 py-2.5 text-sm text-right text-slate-300">{RUPEE}{p.grossAmount.toLocaleString('en-IN')}</td>
                    <td className="px-4 py-2.5 text-sm text-right text-rose-400/80">{p.deductions > 0 ? `-${RUPEE}${p.deductions.toLocaleString('en-IN')}` : <span className="text-slate-600">-</span>}</td>
                    <td className="px-4 py-2.5 text-sm text-right text-emerald-400/80">+{RUPEE}{p.allowances.toLocaleString('en-IN')}</td>
                    <td className="px-4 py-2.5 text-sm text-right font-semibold text-slate-100">{RUPEE}{p.netAmount.toLocaleString('en-IN')}</td>
                    <td className="px-4 py-2.5 text-center">{statusBadge(p.status)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {loadingMore && (
            <div className="text-center py-3 border-t border-slate-800/50">
              <div className="inline-flex items-center gap-2 text-xs text-slate-500">
                <div className="w-3.5 h-3.5 border-2 border-slate-700 border-t-indigo-400 rounded-full animate-spin"></div>
                Loading more...
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
