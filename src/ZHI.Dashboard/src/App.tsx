import { useState, useEffect } from 'react'
import { useWebSocket } from './hooks/useWebSocket'
import { useStats } from './hooks/useStats'
import { useEcoHistory } from './hooks/useEcoHistory'
import { WorldMap } from './components/WorldMap'
import { AgentCardsPanel } from './components/AgentCardsPanel'
import { LogPanel } from './components/LogPanel'
import { ChartsPanel } from './components/ChartsPanel'
import { EventMonitor } from './components/EventMonitor'
import { EventLog } from './components/EventLog'

function App() {
  const { generation, totalDeaths, agents, food, corpses, river, scent, signalField, logs, events, stats, connected } = useWebSocket()
  const { stats: dbStats, loading } = useStats()
  const { history, record } = useEcoHistory()
  const [bottomTab, setBottomTab] = useState<'log' | 'charts' | 'events'>('log')
  const [trackedAgent, setTrackedAgent] = useState<number | null>(null)

  // Display toggles
  const [showScent, setShowScent] = useState(false)
  const [showFoodScent, setShowFoodScent] = useState(false)
  const [showDirection, setShowDirection] = useState(true)
  const [showVision, setShowVision] = useState(false)
  const [showSignal, setShowSignal] = useState(false)

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
        <span className="text-neutral-600">栀 · Cosmos V2.7</span>
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
          {!loading && dbStats && (
            <>
              <span className="text-[10px] text-neutral-500">|</span>
              <span className="text-[10px] text-neutral-500">
                AvgLife {dbStats.avg_alive_seconds_recent_10.toFixed(0)}s
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
        {/* Left Sidebar: Event Monitor */}
        <aside className="w-64 shrink-0 border-r border-neutral-800 flex flex-col min-h-0">
          <EventMonitor events={events} energySource={stats?.energy_source} />
        </aside>

        {/* Center: Map top + Bottom panel */}
        <div className="flex-1 flex flex-col min-h-0 min-w-0">
          {/* Display toggles bar */}
          <div className="flex items-center gap-1 px-3 py-1 border-b border-neutral-800 shrink-0">
            <span className="text-neutral-600 text-[9px] mr-1">显示:</span>
            <button
              onClick={() => setShowScent(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showScent ? 'border-blue-600 text-blue-400 bg-blue-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              气味-栀
            </button>
            <button
              onClick={() => setShowFoodScent(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showFoodScent ? 'border-green-600 text-green-400 bg-green-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              气味-食
            </button>
            <button
              onClick={() => setShowDirection(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showDirection ? 'border-yellow-600 text-yellow-400 bg-yellow-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              方向
            </button>
            <button
              onClick={() => setShowVision(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showVision ? 'border-purple-600 text-purple-400 bg-purple-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              视野
            </button>
            <button
              onClick={() => setShowSignal(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showSignal ? 'border-yellow-600 text-yellow-400 bg-yellow-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              信号
            </button>
            {stats && (
              <>
                <span className="text-neutral-700 text-[9px] ml-2">|</span>
                <span className="text-neutral-600 text-[9px]">ATK:{stats.attack_rate.toFixed(2)}/t</span>
              </>
            )}
          </div>
          <div className="flex-1 min-h-0 border-r border-neutral-800">
            <WorldMap
              agents={agents}
              food={food}
              corpses={corpses}
              river={river}
              scent={scent}
              signalField={signalField}
              events={events}
              trackedAgent={trackedAgent}
              onTrackChange={setTrackedAgent}
              showScent={showScent}
              showFoodScent={showFoodScent}
              showDirection={showDirection}
              showVision={showVision}
              showSignal={showSignal}
            />
          </div>
          <div className="h-48 shrink-0 border-t border-r border-neutral-800 flex flex-col overflow-hidden">
            {/* Tab toggle */}
            <div className="flex items-center gap-1 px-3 py-1 border-b border-neutral-800 shrink-0">
              {(['log', 'charts', 'events'] as const).map(tab => (
                <button
                  key={tab}
                  className={`px-2 py-0.5 text-[10px] rounded ${bottomTab === tab ? 'bg-neutral-800 text-neutral-300' : 'text-neutral-600 hover:text-neutral-400'}`}
                  onClick={() => setBottomTab(tab)}
                >
                  {tab === 'log' ? 'Log' : tab === 'charts' ? 'Charts' : 'Events'}
                </button>
              ))}
            </div>
            {bottomTab === 'charts' ? <ChartsPanel data={history} />
              : bottomTab === 'events' ? <EventLog events={events} />
              : <LogPanel logs={logs} />}
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
