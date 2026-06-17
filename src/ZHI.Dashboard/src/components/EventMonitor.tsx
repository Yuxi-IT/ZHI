import { useEffect, useRef } from 'react'
import type { WorldEvent, EnergySource } from '../types'

interface Props {
  events: WorldEvent[]
  energySource?: EnergySource | null
}

const EVENT_COLORS: Record<string, string> = {
  eat: 'text-green-400',
  attack: 'text-red-400',
  death: 'text-neutral-500',
  reproduce: 'text-purple-400',
  hide_enter: 'text-blue-400',
  hide_exit: 'text-cyan-400',
  signal: 'text-yellow-400',
}

function formatEvent(e: WorldEvent): string {
  switch (e.type) {
    case 'eat':
      return `#${e.agent_id} ate ${e.food_type ?? '?'} (+${e.value.toFixed(1)} HP)`
    case 'attack':
      return `A#${e.agent_id} → T#${e.target_id} (-${e.value.toFixed(1)} stress)`
    case 'death':
      return `#${e.agent_id} DEAD`
    case 'reproduce':
      return `#${e.agent_id} → #${e.child_id}`
    case 'hide_enter':
      return `#${e.agent_id} HIDING`
    case 'hide_exit':
      return `#${e.agent_id} REVEALED`
    case 'signal':
      return `#${e.agent_id} signal(${e.signal_value})`
    default:
      return JSON.stringify(e)
  }
}

export function EventMonitor({ events, energySource }: Props) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const autoScrollRef = useRef(true)

  useEffect(() => {
    const el = scrollRef.current
    if (!el) return
    if (autoScrollRef.current) {
      el.scrollTop = el.scrollHeight
    }
  }, [events])

  const handleScroll = () => {
    const el = scrollRef.current
    if (!el) return
    autoScrollRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 40
  }

  return (
    <div className="h-full flex flex-col min-h-0">
      {/* Header */}
      <div className="px-3 py-1.5 border-b border-neutral-800 shrink-0">
        <div className="text-neutral-500 text-[10px] flex items-center justify-between">
          <span>events ({events.length})</span>
        </div>
        {energySource && (
          <div className="text-[9px] text-neutral-600 mt-0.5 flex gap-2">
            <span>F:{energySource.food_pct.toFixed(0)}%</span>
            <span>B:{energySource.bigfood_pct.toFixed(0)}%</span>
            <span>C:{energySource.corpse_pct.toFixed(0)}%</span>
          </div>
        )}
      </div>

      {/* Event list */}
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto px-2 py-1 space-y-0.5 text-[10px] font-mono"
      >
        {events.map((e, i) => (
          <div key={i} className={EVENT_COLORS[e.type] ?? 'text-neutral-400'}>
            <span className="text-neutral-600 mr-1">{e.tick}</span>
            {formatEvent(e)}
          </div>
        ))}
      </div>
    </div>
  )
}
