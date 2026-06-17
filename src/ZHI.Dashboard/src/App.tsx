import { useState, useEffect } from 'react'
import { useWebSocket } from './hooks/useWebSocket'
import { useStats } from './hooks/useStats'
import { useEcoHistory } from './hooks/useEcoHistory'
import { WorldMap } from './components/WorldMap'
import { AgentCardsPanel } from './components/AgentCardsPanel'
import { LogPanel } from './components/LogPanel'
import { ChartsPanel } from './components/ChartsPanel'

function App() {
  const { generation, totalDeaths, agents, food, corpses, logs, connected } = useWebSocket()
  const { stats, loading } = useStats()
  const { history, record } = useEcoHistory()
  const [showCharts, setShowCharts] = useState(false)
  const [trackedAgent, setTrackedAgent] = useState<number | null>(null)

  const aliveCount = agents.filter(a => a.is_alive).length

  // Record eco history on each data update
  useEffect(() => {
    if (agents.length > 0) {
      record(agents, food, corpses, generation)
    }
  }, [agents, food, corpses, generation, record])

  // Clear tracking if agent dies
  useEffect(() => {
    if (trackedAgent !== null) {
      const agent = agents.find(a => a.id === trackedAgent)
      if (agent && !agent.is_alive) setTrackedAgent(null)
    }
  }, [agents, trackedAgent])

  return (
    <div className="h-screen flex flex-col bg-[#0a0a0a] text-neutral-300 font-mono text-xs overflow-hidden">
      {/* Header */}
      <header className="flex items-center gap-4 px-5 py-2 border-b border-neutral-800 shrink-0">
        <h1 className="text-sm font-normal tracking-[0.2em] text-neutral-400">ZHI</h1>
        <span className="text-neutral-600">栀 · Cosmos V2.5</span>
        <div className="flex items-center gap-3 ml-auto">
          <span className="text-[10px] text-neutral-500">Gen {generation}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">Deaths {totalDeaths}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">Alive {aliveCount}/{agents.length}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">Food {food.length}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">Corpses {corpses.length}</span>
          {!loading && stats && (
            <>
              <span className="text-[10px] text-neutral-500">|</span>
              <span className="text-[10px] text-neutral-500">
                AvgLife {stats.avg_alive_seconds_recent_10.toFixed(0)}s
              </span>
            </>
          )}
          <div className="flex items-center gap-2">
            <span className={`w-1.5 h-1.5 rounded-full ${connected ? 'bg-green-500' : 'bg-red-500'}`} />
            <span className="text-[10px] text-neutral-500">{connected ? 'live' : 'off'}</span>
          </div>
        </div>
      </header>

      {/* Main */}
      <div className="flex-1 flex min-h-0">
        {/* Center: Map top + Bottom panel */}
        <div className="flex-1 flex flex-col min-h-0 min-w-0">
          <div className="flex-1 min-h-0 border-r border-neutral-800">
            <WorldMap
              agents={agents}
              food={food}
              corpses={corpses}
              trackedAgent={trackedAgent}
              onTrackChange={setTrackedAgent}
            />
          </div>
          <div className="h-48 shrink-0 border-t border-r border-neutral-800 flex flex-col overflow-hidden">
            {/* Tab toggle */}
            <div className="flex items-center gap-1 px-3 py-1 border-b border-neutral-800 shrink-0">
              <button
                className={`px-2 py-0.5 text-[10px] rounded ${!showCharts ? 'bg-neutral-800 text-neutral-300' : 'text-neutral-600 hover:text-neutral-400'}`}
                onClick={() => setShowCharts(false)}
              >
                Log
              </button>
              <button
                className={`px-2 py-0.5 text-[10px] rounded ${showCharts ? 'bg-neutral-800 text-neutral-300' : 'text-neutral-600 hover:text-neutral-400'}`}
                onClick={() => setShowCharts(true)}
              >
                Charts
              </button>
            </div>
            {showCharts ? <ChartsPanel data={history} /> : <LogPanel logs={logs} />}
          </div>
        </div>

        {/* Right Sidebar: Agent Cards */}
        <aside className="w-80 shrink-0 flex flex-col min-h-0">
          <AgentCardsPanel
            agents={agents}
            trackedId={trackedAgent}
            onTrack={setTrackedAgent}
          />
        </aside>
      </div>
    </div>
  )
}

export default App
