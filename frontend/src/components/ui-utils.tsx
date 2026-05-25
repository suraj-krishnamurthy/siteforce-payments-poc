export function statusBadge(status: string) {
  const styles: Record<string, string> = {
    Ready: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/20',
    Calculated: 'bg-emerald-500/10 text-emerald-400 ring-emerald-500/20',
    Disputed: 'bg-rose-500/10 text-rose-400 ring-rose-500/20',
    Pending: 'bg-amber-500/10 text-amber-400 ring-amber-500/20',
    Approved: 'bg-sky-500/10 text-sky-400 ring-sky-500/20',
  }

  const style = styles[status] || 'bg-slate-500/10 text-slate-400 ring-slate-500/20'

  return (
    <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-semibold ring-1 ${style}`}>
      {status}
    </span>
  )
}

export const RUPEE = '\u20B9'

export function formatCurrency(amount: number): string {
  return `${RUPEE}${amount.toLocaleString('en-IN')}`
}

export function formatLakhs(amount: number): string {
  return `${RUPEE}${(amount / 100000).toFixed(1)}L`
}
