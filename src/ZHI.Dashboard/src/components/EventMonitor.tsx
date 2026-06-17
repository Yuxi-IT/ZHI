import { useEffect, useRef, useState, useMemo } from 'react'
import type { WorldEvent, WorldEventType, EnergySource } from '../types'
import { useT } from '../i18n/I18nContext'

interface Props {
  events: WorldEvent[]
  energySource?: EnergySource | null
  onClear?: () => void
}

const EVENT_COLORS: Record<string, string> = {
  eat: 'text-green-400',
  attack: 'text-red-400',
  death: 'text-neutral-500',
  reproduce: 'text-purple-400',
  signal: 'text-yellow-400',
  respawn: 'text-violet-400',
  push: 'text-amber-400',
  terraform: 'text-stone-400',
  flood: 'text-blue-400',
  weather: 'text-neutral-400',
}

const FILTER_TYPES: WorldEventType[] = ['death', 'attack', 'respawn', 'eat', 'signal', 'push', 'terraform', 'flood', 'weather']

const FILTER_COLORS: Record<string, string> = {
  death: 'border-neutral-500 text-neutral-400',
  attack: 'border-red-600 text-red-400',
  respawn: 'border-violet-600 text-violet-400',
  eat: 'border-green-600 text-green-400',
  signal: 'border-yellow-600 text-yellow-400',
  push: 'border-amber-600 text-amber-400',
  terraform: 'border-stone-600 text-stone-400',
  flood: 'border-blue-600 text-blue-400',
  weather: 'border-neutral-600 text-neutral-400',
}

export function EventMonitor({ events, energySource, onClear }: Props) {
  const { t } = useT()
  const scrollRef = useRef<HTMLDivElement>(null)
  const autoScrollRef = useRef(true)
  const [autoScroll, setAutoScroll] = useState(true)
  const [activeFilters, setActiveFilters] = useState<Set<WorldEventType>>(
    new Set(['death', 'attack', 'respawn', 'eat'])
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
    if (autoScroll) {
      el.scrollTop = el.scrollHeight
    }
  }, [filteredEvents, autoScroll])

  const handleScroll = () => {
    const el = scrollRef.current
    if (!el) return
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40
    autoScrollRef.current = atBottom
    if (atBottom !== autoScroll) setAutoScroll(atBottom)
  }

  const formatEvent = (e: WorldEvent): string => {
    switch (e.type) {
      case 'eat':
        return t('events.ate', { id: e.agent_id, type: e.food_type ?? '?', val: e.value.toFixed(1) })
      case 'attack':
        return t('events.attacked', { attacker: e.agent_id, target: e.target_id ?? '?', val: e.value.toFixed(1) })
      case 'death':
        return t('events.dead', { id: e.agent_id })
      case 'reproduce':
        return t('events.reproduced', { parent: e.agent_id, child: e.child_id ?? '?' })
      case 'signal':
        return t('events.signaled', { id: e.agent_id, val: e.signal_value ?? 0 })
      case 'respawn':
        return t('events.respawned', { id: e.agent_id })
      case 'push':
        return t('events.pushed', { id: e.agent_id, type: e.food_type ?? '?' })
      case 'terraform':
        return t('events.terraformed', { id: e.agent_id })
      case 'flood':
        return t('events.flooded')
      case 'weather':
        return t('events.weathered', { type: e.food_type ?? '?' })
      default:
        return JSON.stringify(e)
    }
  }

  return (
    <div className="h-full flex flex-col min-h-0">
      <div className="px-3 py-1.5 border-b border-neutral-800 shrink-0">
        <div className="text-neutral-500 text-[10px] flex items-center justify-between">
          <span>{t('events.title')} ({filteredEvents.length})</span>
          <div className="flex items-center gap-1">
            <button
              onClick={() => {
                setAutoScroll(v => !v)
                autoScrollRef.current = !autoScrollRef.current
                if (!autoScroll) {
                  const el = scrollRef.current
                  if (el) el.scrollTop = el.scrollHeight
                }
              }}
              className={`px-1 py-0.5 text-[9px] rounded border ${
                autoScroll
                  ? 'border-cyan-700 text-cyan-400'
                  : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'
              }`}
              title={autoScroll ? t('events.autoScrollOn') : t('events.autoScrollOff')}
            >
              {autoScroll ? '⇩' : '⇩'}
            </button>
            {onClear && (
              <button
                onClick={onClear}
                className="px-1 py-0.5 text-[9px] rounded border border-neutral-800 text-neutral-600 hover:text-red-400"
                title={t('events.clearAll')}
              >
                {t('events.clear')}
              </button>
            )}
          </div>
        </div>
        {energySource && (
          <div className="text-[9px] text-neutral-600 mt-0.5 flex gap-2">
            <span>F:{energySource.food_pct.toFixed(0)}%</span>
            <span>B:{energySource.bigfood_pct.toFixed(0)}%</span>
            <span>C:{energySource.corpse_pct.toFixed(0)}%</span>
          </div>
        )}
      </div>

      <div className="px-2 py-1 border-b border-neutral-800 shrink-0 flex flex-wrap gap-1">
        {FILTER_TYPES.map(type => (
          <button
            key={type}
            onClick={() => toggleFilter(type)}
            className={`px-1.5 py-0.5 text-[9px] rounded border transition-colors ${
              activeFilters.has(type)
                ? `${FILTER_COLORS[type]} bg-neutral-800/50`
                : 'border-neutral-800 text-neutral-700'
            }`}
          >
            {t(`events.${type}`)}
          </button>
        ))}
      </div>

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
