import { NavLink } from 'react-router-dom'

const navItems = [
  { to: '/upload', label: 'Upload', icon: '\u{1F4E4}' },
  { to: '/dashboard', label: 'Dashboard', icon: '\u{1F4CA}' },
  { to: '/rules', label: 'Rules', icon: '\u2699\uFE0F' },
  { to: '/audit', label: 'Audit Trail', icon: '\u{1F4CB}' },
]

export default function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-screen bg-[#0f1117]">
      {/* Sidebar */}
      <aside className="w-56 bg-[#13151d] border-r border-slate-800/50 text-white flex flex-col">
        <div className="px-4 py-4 border-b border-slate-800/50">
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 bg-indigo-600 rounded-md flex items-center justify-center text-xs font-bold tracking-tight">
              SF
            </div>
            <div>
              <h1 className="text-sm font-bold tracking-tight">SiteForce</h1>
              <p className="text-[10px] text-slate-500 uppercase tracking-widest">Payments</p>
            </div>
          </div>
        </div>
        <nav className="flex-1 px-2.5 py-3 space-y-0.5">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex items-center gap-2.5 px-3 py-2.5 rounded-lg text-sm font-medium transition-all duration-150 ${
                  isActive
                    ? 'bg-indigo-600/15 text-indigo-400 ring-1 ring-indigo-500/20'
                    : 'text-slate-400 hover:bg-slate-800/40 hover:text-slate-300'
                }`
              }
            >
              <span className="text-base">{item.icon}</span>
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <div className="px-4 py-3 border-t border-slate-800/50">
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 bg-slate-700/60 rounded-full flex items-center justify-center text-xs font-medium text-slate-400">
              D
            </div>
            <span className="text-sm text-slate-500">demo-user</span>
          </div>
        </div>
      </aside>

      {/* Main content */}
      <main className="flex-1 overflow-auto">
        <div className="p-6">
          {children}
        </div>
      </main>
    </div>
  )
}
