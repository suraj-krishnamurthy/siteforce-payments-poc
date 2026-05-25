import { useEffect, useMemo, useState } from 'react'
import api from '../api/client'

const RUPEE = '\u20B9'

interface SiteRuleConfig {
  id: number
  siteName: string
  advanceDeductionAmount: number
  siteAllowancePercent: number
  disputeThresholdAmount: number
  updatedAt: string
  updatedBy: string
}

interface PaginatedResponse {
  items: { siteName: string }[]
}

export default function RulesPage() {
  const [rules, setRules] = useState<SiteRuleConfig[]>([])
  const [globalDefault, setGlobalDefault] = useState<SiteRuleConfig | null>(null)
  const [availableSites, setAvailableSites] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [showForm, setShowForm] = useState(false)
  const [editingRule, setEditingRule] = useState<SiteRuleConfig | null>(null)
  const [form, setForm] = useState({
    siteName: '',
    advanceDeductionAmount: 2000,
    siteAllowancePercent: 10,
    disputeThresholdAmount: 20358,
  })
  const [saving, setSaving] = useState(false)

  const fetchRules = async () => {
    setLoading(true)
    try {
      const [rulesRes, defaultsRes, paymentsRes] = await Promise.all([
        api.get<SiteRuleConfig[]>('/rules'),
        api.get<SiteRuleConfig>('/rules/defaults'),
        api.get<PaginatedResponse>('/payments', { params: { pageSize: 10000 } }),
      ])
      setRules(rulesRes.data)
      setGlobalDefault(defaultsRes.data)

      const allSites = [...new Set([
        ...paymentsRes.data.items.map((p) => p.siteName),
        ...rulesRes.data.map((r) => r.siteName),
      ])].sort((a, b) => a.localeCompare(b))
      setAvailableSites(allSites)
    } catch {
      // ignore
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchRules()
  }, [])

  const normalizedSiteName = form.siteName.trim().toLowerCase()
  const existingRuleForSite = useMemo(
    () => rules.find((r) => r.siteName.trim().toLowerCase() === normalizedSiteName),
    [rules, normalizedSiteName]
  )

  const handleEdit = (rule: SiteRuleConfig) => {
    setEditingRule(rule)
    setForm({
      siteName: rule.siteName,
      advanceDeductionAmount: rule.advanceDeductionAmount,
      siteAllowancePercent: rule.siteAllowancePercent,
      disputeThresholdAmount: rule.disputeThresholdAmount,
    })
    setShowForm(true)
  }

  const handleAdd = () => {
    setEditingRule(null)
    setForm({
      siteName: '',
      advanceDeductionAmount: globalDefault?.advanceDeductionAmount ?? 0,
      siteAllowancePercent: globalDefault?.siteAllowancePercent ?? 10,
      disputeThresholdAmount: globalDefault?.disputeThresholdAmount ?? 20358,
    })
    setShowForm(true)
  }

  const handleSave = async () => {
    const siteName = form.siteName.trim()

    if (!siteName) {
      alert('Site name is required')
      return
    }

    if (!editingRule && existingRuleForSite) {
      alert(`A rule already exists for "${existingRuleForSite.siteName}". Use Edit instead.`)
      return
    }

    setSaving(true)
    try {
      await api.post('/rules', { ...form, siteName })
      setShowForm(false)
      setEditingRule(null)
      fetchRules()
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Save failed')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (siteName: string) => {
    if (!confirm(`Delete rule configuration for "${siteName}"? Global defaults will apply.`)) return
    try {
      await api.delete(`/rules/${siteName}`)
      fetchRules()
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Delete failed')
    }
  }

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <div className="w-8 h-8 border-3 border-slate-700 border-t-indigo-500 rounded-full animate-spin"></div>
    </div>
  )

  return (
    <div>
      <div className="flex justify-between items-center mb-8">
        <div>
          <h2 className="text-2xl font-bold text-slate-100">Rules Configuration</h2>
          <p className="text-sm text-slate-500 mt-1">Configure payment rules per site</p>
        </div>
        <button
          onClick={handleAdd}
          className="px-4 py-2.5 bg-indigo-600 text-white text-sm font-medium rounded-lg hover:bg-indigo-500 active:scale-[0.98] transition-all"
        >
          + Add Site Rule
        </button>
      </div>

      <div className="overflow-x-auto bg-slate-900 border border-slate-800 rounded-xl">
        <table className="min-w-full">
          <thead className="bg-slate-800/50 border-b border-slate-700">
            <tr>
              <th className="px-4 py-3 text-left text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Site</th>
              <th className="px-4 py-3 text-right text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Advance Recovery ({RUPEE})</th>
              <th className="px-4 py-3 text-right text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Site Allowance (%)</th>
              <th className="px-4 py-3 text-right text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Dispute Threshold ({RUPEE})</th>
              <th className="px-4 py-3 text-center text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Updated</th>
              <th className="px-4 py-3 text-center text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800/50">
            {globalDefault && (
              <tr className="bg-indigo-950/30">
                <td className="px-4 py-3.5 text-sm font-semibold text-indigo-300">
                  {globalDefault.siteName}
                  <span className="ml-2 text-[10px] font-normal text-indigo-400 bg-indigo-900/50 px-1.5 py-0.5 rounded ring-1 ring-indigo-500/20">DEFAULT</span>
                </td>
                <td className="px-4 py-3.5 text-sm text-right font-medium text-slate-300">{RUPEE}{globalDefault.advanceDeductionAmount.toLocaleString('en-IN')}</td>
                <td className="px-4 py-3.5 text-sm text-right font-medium text-slate-300">{globalDefault.siteAllowancePercent}%</td>
                <td className="px-4 py-3.5 text-sm text-right font-medium text-slate-300">{RUPEE}{globalDefault.disputeThresholdAmount.toLocaleString('en-IN')}</td>
                <td className="px-4 py-3.5 text-xs text-center text-slate-500">System</td>
                <td className="px-4 py-3.5 text-xs text-center text-slate-600">-</td>
              </tr>
            )}
            {rules.map((r) => (
              <tr key={r.id} className="hover:bg-slate-800/30 transition-colors">
                <td className="px-4 py-3.5 text-sm font-medium text-slate-300">{r.siteName}</td>
                <td className="px-4 py-3.5 text-sm text-right text-slate-400">{RUPEE}{r.advanceDeductionAmount.toLocaleString('en-IN')}</td>
                <td className="px-4 py-3.5 text-sm text-right text-slate-400">{r.siteAllowancePercent}%</td>
                <td className="px-4 py-3.5 text-sm text-right text-slate-400">{RUPEE}{r.disputeThresholdAmount.toLocaleString('en-IN')}</td>
                <td className="px-4 py-3.5 text-xs text-center text-slate-500">
                  {new Date(r.updatedAt).toLocaleDateString()} by {r.updatedBy}
                </td>
                <td className="px-4 py-3.5 text-center space-x-2">
                  <button
                    onClick={() => handleEdit(r)}
                    className="px-2.5 py-1 text-xs font-medium text-indigo-400 hover:bg-indigo-900/30 rounded-md transition-colors"
                  >
                    Edit
                  </button>
                  <button
                    onClick={() => handleDelete(r.siteName)}
                    className="px-2.5 py-1 text-xs font-medium text-red-400 hover:bg-red-900/30 rounded-md transition-colors"
                  >
                    Delete
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {rules.length === 0 && (
        <p className="text-xs text-slate-500 mt-2 text-center">No site-specific overrides configured. All sites use the global default above.</p>
      )}

      {showForm && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-slate-900 rounded-2xl p-6 w-full max-w-md shadow-2xl border border-slate-800">
            <h3 className="text-lg font-bold text-slate-100 mb-5">
              {editingRule ? `Edit Rules for ${editingRule.siteName}` : 'Add Site Rule'}
            </h3>
            <div className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">Site Name</label>
                {editingRule ? (
                  <input
                    type="text"
                    value={form.siteName}
                    disabled
                    className="w-full border border-slate-700 rounded-lg px-3 py-2.5 text-sm bg-slate-800/50 text-slate-500 cursor-not-allowed"
                  />
                ) : (
                  <>
                    <input
                      type="text"
                      list="site-name-suggestions"
                      value={form.siteName}
                      onChange={(e) => setForm({ ...form, siteName: e.target.value })}
                      placeholder="Enter site name"
                      className="w-full border border-slate-700 rounded-lg px-3 py-2.5 text-sm bg-slate-800 text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500/30 focus:border-indigo-500"
                    />
                    <datalist id="site-name-suggestions">
                      {availableSites.map((site) => (
                        <option key={site} value={site} />
                      ))}
                    </datalist>
                  </>
                )}
                <p className="text-[11px] text-slate-500 mt-1">You can type a new site name before any upload, or pick a known site suggestion.</p>
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">Advance Recovery ({RUPEE})</label>
                <input
                  type="number"
                  value={form.advanceDeductionAmount}
                  onChange={(e) => setForm({ ...form, advanceDeductionAmount: parseFloat(e.target.value) || 0 })}
                  className="w-full border border-slate-700 rounded-lg px-3 py-2.5 text-sm bg-slate-800 text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500/30 focus:border-indigo-500"
                />
                <p className="text-[11px] text-slate-500 mt-1">Flat advance recovery per worker per period. Set 0 for none.</p>
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">Site Allowance (%)</label>
                <input
                  type="number"
                  value={form.siteAllowancePercent}
                  onChange={(e) => setForm({ ...form, siteAllowancePercent: parseFloat(e.target.value) || 0 })}
                  className="w-full border border-slate-700 rounded-lg px-3 py-2.5 text-sm bg-slate-800 text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500/30 focus:border-indigo-500"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1.5">Dispute Threshold ({RUPEE})</label>
                <input
                  type="number"
                  value={form.disputeThresholdAmount}
                  onChange={(e) => setForm({ ...form, disputeThresholdAmount: parseFloat(e.target.value) || 0 })}
                  className="w-full border border-slate-700 rounded-lg px-3 py-2.5 text-sm bg-slate-800 text-slate-200 focus:outline-none focus:ring-2 focus:ring-indigo-500/30 focus:border-indigo-500"
                />
                <p className="text-[11px] text-slate-500 mt-1">Workers with net pay below this will be flagged.</p>
              </div>
            </div>
            <div className="flex gap-3 justify-end mt-6 pt-4 border-t border-slate-800">
              <button
                onClick={() => { setShowForm(false); setEditingRule(null) }}
                className="px-4 py-2.5 text-sm font-medium text-slate-300 bg-slate-800 rounded-lg hover:bg-slate-700 transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={handleSave}
                disabled={saving}
                className="px-5 py-2.5 text-sm font-medium bg-indigo-600 text-white rounded-lg hover:bg-indigo-500 disabled:opacity-50 active:scale-[0.98] transition-all"
              >
                {saving ? 'Saving...' : 'Save'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
