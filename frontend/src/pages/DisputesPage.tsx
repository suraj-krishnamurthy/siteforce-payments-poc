import { useEffect, useState } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import api from '../api/client'

interface DisputeItem {
  id: number
  paymentLineId: number
  workerId: string
  siteName: string
  raisedBy: string
  reason: string
  description: string
  status: string
  createdAt: string
  resolvedBy: string | null
  resolvedAt: string | null
  resolutionNotes: string | null
}

export default function DisputesPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const [disputes, setDisputes] = useState<DisputeItem[]>([])
  const [loading, setLoading] = useState(true)
  const [filterStatus, setFilterStatus] = useState(searchParams.get('status') || '')
  const [filterSite, setFilterSite] = useState(searchParams.get('site') || '')
  const [sites, setSites] = useState<string[]>([])

  const fetchData = async () => {
    setLoading(true)
    try {
      const res = await api.get<DisputeItem[]>('/disputes', {
        params: {
          ...(filterStatus && { status: filterStatus }),
        },
      })
      let filtered = res.data
      if (filterSite) {
        filtered = filtered.filter((d) => d.siteName === filterSite)
      }
      setDisputes(filtered)

      // Build sites list from all disputes
      const allSites = [...new Set(res.data.map((d) => d.siteName))]
      setSites(allSites)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    // Read from URL params on mount
    const siteParam = searchParams.get('site')
    const statusParam = searchParams.get('status')
    if (siteParam) setFilterSite(siteParam)
    if (statusParam) setFilterStatus(statusParam)
  }, [])

  useEffect(() => {
    fetchData()
  }, [filterStatus, filterSite])

  const handleStatusChange = (value: string) => {
    setFilterStatus(value)
    updateUrlParams(filterSite, value)
  }

  const handleSiteChange = (value: string) => {
    setFilterSite(value)
    updateUrlParams(value, filterStatus)
  }

  const updateUrlParams = (site: string, status: string) => {
    const params: Record<string, string> = {}
    if (site) params.site = site
    if (status) params.status = status
    setSearchParams(params, { replace: true })
  }

  const clearFilters = () => {
    setFilterSite('')
    setFilterStatus('')
    setSearchParams({}, { replace: true })
  }

  const handleResolve = async (id: number) => {
    const notes = prompt('Resolution notes:')
    if (notes === null) return
    try {
      await api.post(`/disputes/${id}/resolve`, { resolutionNotes: notes })
      fetchData()
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Resolve failed')
    }
  }

  if (loading) return <div className="text-gray-500">Loading...</div>

  return (
    <div>
      <h2 className="text-2xl font-bold mb-6">Disputes</h2>

      <div className="flex gap-4 mb-4 items-center">
        <select
          value={filterSite}
          onChange={(e) => handleSiteChange(e.target.value)}
          className="border rounded px-3 py-1.5 text-sm"
        >
          <option value="">All Sites</option>
          {sites.map((s) => (
            <option key={s} value={s}>{s}</option>
          ))}
        </select>
        <select
          value={filterStatus}
          onChange={(e) => handleStatusChange(e.target.value)}
          className="border rounded px-3 py-1.5 text-sm"
        >
          <option value="">All Statuses</option>
          <option value="Open">Open</option>
          <option value="Resolved">Resolved</option>
        </select>
        {(filterSite || filterStatus) && (
          <button
            onClick={clearFilters}
            className="text-xs text-blue-600 hover:underline"
          >
            Clear filters
          </button>
        )}
        <span className="text-xs text-gray-500">{disputes.length} dispute(s)</span>
      </div>

      {/* Disputes List */}
      {disputes.length === 0 ? (
        <p className="text-gray-500 text-sm">No disputes found.</p>
      ) : (
        <div className="space-y-3">
          {disputes.map((d) => (
            <div key={d.id} className="bg-white border rounded-lg p-4 shadow-sm">
              <div className="flex justify-between items-start">
                <div>
                  <p className="font-medium">
                    {d.workerId} — {d.siteName}
                  </p>
                  <p className="text-sm text-gray-600 mt-1">{d.description}</p>
                  <div className="flex gap-4 mt-2 text-xs text-gray-500">
                    <span>Reason: <strong>{d.reason}</strong></span>
                    <span>Raised by: {d.raisedBy}</span>
                    <span>{new Date(d.createdAt).toLocaleDateString()}</span>
                  </div>
                </div>
                <div className="flex flex-col items-end gap-2">
                  <span
                    className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                      d.status === 'Open' ? 'bg-red-100 text-red-800' : 'bg-green-100 text-green-800'
                    }`}
                  >
                    {d.status}
                  </span>
                  {d.status === 'Open' && (
                    <button
                      onClick={() => handleResolve(d.id)}
                      className="text-xs text-blue-600 hover:underline"
                    >
                      Resolve
                    </button>
                  )}
                </div>
              </div>
              {d.resolvedBy && (
                <div className="mt-2 pt-2 border-t text-xs text-gray-500">
                  Resolved by {d.resolvedBy} on {new Date(d.resolvedAt!).toLocaleDateString()}
                  {d.resolutionNotes && <span> — "{d.resolutionNotes}"</span>}
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
