import { useState, useMemo } from 'react'
import type { AgentSnapshot } from '../types'

interface Props {
  agents: AgentSnapshot[]
  onTrack?: (id: number | null) => void
  trackedId?: number | null
}

type SortMode = 'none' | 'hp-desc' | 'hp-asc'

export function AgentCardsPanel({ agents, onTrack, trackedId }: Props) {
  const [pinnedIds, setPinnedIds] = useState<Set<number>>(new Set())
  const [sortMode, setSortMode] = useState<SortMode>('none')

  const togglePin = (id: number) => {
    setPinnedIds(prev => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const aliveAgents = agents.filter(a => a.is_alive)
  const deadAgents = agents.filter(a => !a.is_alive)

  const sortedAlive = useMemo(() => {
    const pinned = aliveAgents.filter(a => pinnedIds.has(a.id))
    const rest = aliveAgents.filter(a => !pinnedIds.has(a.id))

    if (sortMode === 'hp-desc') rest.sort((a, b) => b.existence - a.existence)
    else if (sortMode === 'hp-asc') rest.sort((a, b) => a.existence - b.existence)

    return [...pinned, ...rest]
  }, [aliveAgents, pinnedIds, sortMode])

  const cycleSort = () => {
    setSortMode(prev =>
      prev === 'none' ? 'hp-desc' : prev === 'hp-desc' ? 'hp-asc' : 'none'
    )
  }

  const sortLabel = sortMode === 'hp-desc' ? 'HP ↓' : sortMode === 'hp-asc' ? 'HP ↑' : 'Sort'
  const sortTitle = sortMode === 'none' ? 'Click: HP high→low' : sortMode === 'hp-desc' ? 'Click: HP low→high' : 'Click: clear sort'

  return (
    <div className="h-full flex flex-col min-h-0">
      {/* Toolbar */}
      <div className="flex items-center gap-2 px-3 py-1.5 border-b border-neutral-800 shrink-0">
        <span className="text-neutral-500 text-[10px]">
          Alive {aliveAgents.length}/{agents.length}
        </span>
        {pinnedIds.size > 0 && (
          <span className="text-[9px] text-yellow-600/70">Pin:{pinnedIds.size}</span>
        )}
        <div className="ml-auto flex items-center gap-1">
          <button
            onClick={cycleSort}
            title={sortTitle}
            className={`px-1.5 py-0.5 text-[9px] rounded border ${
              sortMode !== 'none'
                ? 'border-neutral-600 text-neutral-300 bg-neutral-800'
                : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'
            }`}
          >
            {sortLabel}
          </button>
          {pinnedIds.size > 0 && (
            <button
              onClick={() => setPinnedIds(new Set())}
              className="px-1.5 py-0.5 text-[9px] rounded border border-neutral-800 text-neutral-600 hover:text-neutral-400"
              title="Clear all pins"
            >
              Unpin all
            </button>
          )}
        </div>
      </div>

      {/* Agent list */}
      <div className="flex-1 overflow-y-auto p-2 space-y-1.5">
        {sortedAlive.map(agent => (
          <AgentCard
            key={agent.id}
            agent={agent}
            pinned={pinnedIds.has(agent.id)}
            tracked={agent.id === trackedId}
            onTogglePin={() => togglePin(agent.id)}
            onTrack={() => onTrack?.(agent.id === trackedId ? null : agent.id)}
          />
        ))}

        {deadAgents.length > 0 && (
          <div className="pt-2 border-t border-neutral-800">
            <div className="text-neutral-600 text-[10px] px-1 mb-1">Dead ({deadAgents.length})</div>
            {deadAgents.slice(-10).map(agent => (
              <div
                key={agent.id}
                className="rounded border border-neutral-800/50 bg-neutral-900/30 p-1.5 text-[10px] mb-1 opacity-60"
              >
                <span className="text-neutral-600">#{agent.id}</span>
                <span className="text-neutral-700 ml-2">{agent.status}</span>
                <span className="text-neutral-700 ml-2">{agent.tick_count}t</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}

function AgentCard({
  agent, pinned, tracked, onTogglePin, onTrack,
}: {
  agent: AgentSnapshot
  pinned: boolean
  tracked: boolean
  onTogglePin: () => void
  onTrack: () => void
}) {
  const hp = Math.max(0, Math.min(1, agent.existence / 100))
  const hpColor = `hsl(${hp * 120}, 70%, 50%)`

  return (
    <div
      className={`rounded border p-2 text-[10px] transition-colors ${
        tracked
          ? 'border-yellow-500/50 bg-yellow-900/10'
          : pinned
          ? 'border-neutral-600 bg-neutral-900/70'
          : 'border-neutral-800 bg-neutral-900/50'
      }`}
    >
      <div className="flex items-center justify-between mb-1">
        <div className="flex items-center gap-1.5">
          <button
            onClick={onTogglePin}
            className={`text-[9px] leading-none ${pinned ? 'text-yellow-500' : 'text-neutral-700 hover:text-neutral-500'}`}
            title={pinned ? 'Unpin' : 'Pin to top'}
          >
            {pinned ? '◉' : '○'}
          </button>
          <span className="text-neutral-300 font-medium">#{agent.id}</span>
          {agent.is_hiding && <span className="text-blue-400 text-[8px]">HIDDEN</span>}
        </div>
        <div className="flex items-center gap-1.5">
          <button
            onClick={onTrack}
            className={`text-[9px] leading-none ${tracked ? 'text-yellow-400' : 'text-neutral-700 hover:text-neutral-500'}`}
            title={tracked ? 'Stop tracking' : 'Track on map'}
          >
            {tracked ? '◆' : '◇'}
          </button>
          <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: hpColor }} />
        </div>
      </div>
      <div className="text-neutral-500 space-y-0.5">
        <div className="flex justify-between">
          <span>HP</span>
          <span className="text-neutral-400">{agent.existence.toFixed(1)}</span>
        </div>
        <div className="flex justify-between">
          <span>Stress</span>
          <span className={agent.stress > 1 ? 'text-red-400' : 'text-neutral-400'}>
            {agent.stress.toFixed(2)}
          </span>
        </div>
        <div className="flex justify-between">
          <span>Thirst</span>
          <span className={agent.thirst < 20 ? 'text-cyan-400' : 'text-neutral-400'}>
            {agent.thirst.toFixed(1)}
          </span>
        </div>
        <div className="flex justify-between">
          <span>Age</span>
          <span className="text-neutral-400">{agent.tick_count}t</span>
        </div>
        <div className="flex justify-between">
          <span>Action</span>
          <span className="text-neutral-400 truncate max-w-24">{agent.last_action || 'none'}</span>
        </div>
        <div className="flex justify-between">
          <span>Pos</span>
          <span className="text-neutral-400">({agent.x},{agent.y})</span>
        </div>
        <div className="flex gap-3 mt-0.5 text-neutral-600">
          <span title="Food">F:{agent.food_eat_count}</span>
          <span title="BigFood">B:{agent.bigfood_eat_count}</span>
          <span title="Corpse">C:{agent.corpse_eat_count}</span>
          <span title="Attacks">A:{agent.attack_count}</span>
          <span title="Signals">S:{agent.signal_count}</span>
        </div>
      </div>
    </div>
  )
}
