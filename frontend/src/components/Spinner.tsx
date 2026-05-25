export default function Spinner({ text }: { text?: string }) {
  return (
    <div className="flex items-center justify-center h-64">
      <div className="flex flex-col items-center gap-3">
        <div className="w-7 h-7 border-2 border-slate-700 border-t-indigo-400 rounded-full animate-spin"></div>
        {text && <p className="text-sm text-slate-500">{text}</p>}
      </div>
    </div>
  )
}
