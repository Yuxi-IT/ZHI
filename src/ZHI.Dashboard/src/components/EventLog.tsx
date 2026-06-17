import { useRef, useEffect } from 'react'
import type { WorldEvent } from '../types'

interface Props {
  events: WorldEvent[]
}

const COLORS: Record<string, string> = {
  eat: 'text-green-400',
  attack: 'text-red-400',
  death: 'text-neutral-500',
}

function fmt(e: WorldEvent): string {
  switch (e.type) {
    case 'eat':
      return `#${e.agent_id} ate ${e.food_type ?? '?'} (+${e.value.toFixed(1)} HP)`
    case 'attack':
      return `A#${e.agent_id} → T#${e.target_id} (-${e.value.toFixed(1)} stress)`
    case 'death':
      return `#${e.agent_id} DEAD`
    default:
      return ''
  }
}

export function EventLog({ events }: Props) {
  const ref = useRef<HTMLDivElement>(null)

  const filtered = events.filter(e => e.type === 'eat' || e.type === 'attack' || e.type === 'death')

  useEffect(() => {
    const el = ref.current
    if (el) el.scrollTop = el.scrollHeight
  }, [filtered.length])

  return (
    <div ref={ref} className="flex-1 overflow-y-auto px-2 py-1 space-y-0.5 text-[10px] font-mono">
      {filtered.length === 0 && (
        <div className="text-neutral-700">No events yet</div>
      )}
      {filtered.map((e, i) => (
        <div key={i} className={COLORS[e.type] ?? 'text-neutral-400'}>
          <span className="text-neutral-600 mr-1">{e.tick}</span>
          {fmt(e)}
        </div>
      ))}
    </div>
  )
}
