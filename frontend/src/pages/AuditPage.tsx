import { useEffect, useState } from 'react'
import api from '../api/client'

interface AuditEvent {
  id: number
  eventType: string
  entityType: string
  entityId: string
  actorId: string
  actorName: string
  timestamp: string
  payloadJson: string
}

export default function AuditPage() {
  const [events, setEvents] = useState<AuditEvent[]>([])
  const [expandedId, setExpandedId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const fetch = async () => {
      try {
        const res = await api.get<AuditEvent[]>('/audit')
        setEvents(res.data)
      } catch {
        // ignore
      } finally {
        setLoading(false)
      }
    }
    fetch()
  }, [])

  const eventIcon = (type: string) => {
    if (type.includes('upload')) return '📤'
    if (type.includes('calculated')) return '🧮'
    if (type.includes('approved')) return '✅'
    if (type.includes('dispute_raised')) return '⚠️'
    if (type.includes('dispute_resolved')) return '🔧'
    return '📝'
  }

  if (loading) return (
    <div className="flex items-center justify-center h-64">
      <div className="w-8 h-8 border-3 border-slate-700 border-t-indigo-500 rounded-full animate-spin"></div>
    </div>
  )

  return (
    <div>
      <div className="mb-8">
        <h2 className="text-2xl font-bold text-slate-100">Audit Trail</h2>
        <p className="text-sm text-slate-500 mt-1">Track all system events and actions</p>
      </div>

      {events.length === 0 ? (
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-12 text-center">
          <p className="text-slate-500 text-sm">No audit events yet.</p>
        </div>
      ) : (
        <div className="relative">
          {/* Timeline line */}
          <div className="absolute left-4 top-0 bottom-0 w-0.5 bg-slate-800" />

          <div className="space-y-3">
            {events.map((event) => (
              <div key={event.id} className="relative pl-10">
                {/* Timeline dot */}
                <div className="absolute left-2.5 top-3 w-3 h-3 bg-indigo-500 rounded-full border-2 border-slate-950" />

                <div
                  className="bg-slate-900 border border-slate-800 rounded-lg p-3 cursor-pointer hover:border-slate-700 transition-colors"
                  onClick={() => setExpandedId(expandedId === event.id ? null : event.id)}
                >
                  <div className="flex justify-between items-start">
                    <div className="flex items-center gap-2">
                      <span>{eventIcon(event.eventType)}</span>
                      <span className="font-medium text-sm text-slate-200">{event.eventType}</span>
                    </div>
                    <span className="text-xs text-slate-500">
                      {new Date(event.timestamp).toLocaleString()}
                    </span>
                  </div>
                  <div className="mt-1 text-xs text-slate-500">
                    <span>{event.entityType} #{event.entityId}</span>
                    <span className="mx-2">&bull;</span>
                    <span>by {event.actorName}</span>
                  </div>

                  {/* Expanded payload */}
                  {expandedId === event.id && (
                    <div className="mt-3 p-2 bg-slate-800 rounded text-xs font-mono text-slate-300 overflow-x-auto">
                      <pre>{JSON.stringify(JSON.parse(event.payloadJson), null, 2)}</pre>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
