import { useEffect, useRef, useState, useMemo } from 'react'
import type { WorldEvent, WorldEventType, EnergySource } from '../types'

interface Props {
  events: WorldEvent[]
  energySource?: EnergySource | null
}

const EVENT_COLORS: Record<string, string> = {
  eat: 'text-green-400',
  attack: 'text-red-400',
  death: 'text-neutral-500',
  reproduce: 'text-purple-400',
  signal: 'text-yellow-400',
  respawn: 'text-violet-400',
}

const FILTER_OPTIONS: { type: WorldEventType; label: string; color: string }[] = [
  { type: 'death', label: '死亡', color: 'border-neutral-500 text-neutral-400' },
  { type: 'attack', label: '攻击', color: 'border-red-600 text-red-400' },
  { type: 'respawn', label: '复活', color: 'border-violet-600 text-violet-400' },
  { type: 'eat', label: '进食', color: 'border-green-600 text-green-400' },
  { type: 'signal', label: '信号', color: 'border-yellow-600 text-yellow-400' },
]

function formatEvent(e: WorldEvent): string {
  switch (e.type) {
    case 'eat':
      return `#${e.agent_id} ate ${e.food_type ?? '?'} (+${e.value.toFixed(1)} HP)`
    case 'attack':
      return `A#${e.agent_id} → T#${e.target_id} (-${e.value.toFixed(1)})`
    case 'death':
      return `#${e.agent_id} DEAD`
    case 'reproduce':
      return `#${e.agent_id} → #${e.child_id}`
    case 'signal':
      return `#${e.agent_id} signal(${e.signal_value})`
    case 'respawn':
      return `#${e.agent_id} RESPAWN`
    default:
      return JSON.stringify(e)
  }
}

export function EventMonitor({ events, energySource }: Props) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const autoScrollRef = useRef(true)
  const [activeFilters, setActiveFilters] = useState<Set<WorldEventType>>(
    new Set(['death', 'attack', 'respawn', 'eat', 'signal'])
  )

  const toggleFilter = (type: WorldEventType) => {
    setActiveFilters(prev => {
      const next = new Set(prev)
      if (next.has(type)) next.delete(type)
      else next.add(type)
      return next
    })
  }

  const filteredEvents = useMemo(
    () => events.filter(e => activeFilters.has(e.type)),
    [events, activeFilters]
  )

  useEffect(() => {
    const el = scrollRef.current
    if (!el) return
    if (autoScrollRef.current) {
      el.scrollTop = el.scrollHeight
    }
  }, [filteredEvents])

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
          <span>events ({filteredEvents.length})</span>
        </div>
        {energySource && (
          <div className="text-[9px] text-neutral-600 mt-0.5 flex gap-2">
            <span>F:{energySource.food_pct.toFixed(0)}%</span>
            <span>B:{energySource.bigfood_pct.toFixed(0)}%</span>
            <span>C:{energySource.corpse_pct.toFixed(0)}%</span>
          </div>
        )}
      </div>

      {/* Filters */}
      <div className="px-2 py-1 border-b border-neutral-800 shrink-0 flex flex-wrap gap-1">
        {FILTER_OPTIONS.map(opt => (
          <button
            key={opt.type}
            onClick={() => toggleFilter(opt.type)}
            className={`px-1.5 py-0.5 text-[9px] rounded border transition-colors ${
              activeFilters.has(opt.type)
                ? `${opt.color} bg-neutral-800/50`
                : 'border-neutral-800 text-neutral-700'
            }`}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {/* Event list */}
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto px-2 py-1 space-y-0.5 text-[10px] font-mono"
      >
        {filteredEvents.map((e, i) => (
          <div key={i} className={EVENT_COLORS[e.type] ?? 'text-neutral-400'}>
            <span className="text-neutral-600 mr-1">{e.tick}</span>
            {formatEvent(e)}
          </div>
        ))}
      </div>
    </div>
  )
}
