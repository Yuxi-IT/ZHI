import { useWebSocket } from './hooks/useWebSocket'
import { useStats } from './hooks/useStats'
import { WorldMap } from './components/WorldMap'
import { StatsPanel } from './components/StatsPanel'
import { LogPanel } from './components/LogPanel'

function App() {
  const { generation, totalDeaths, agents, food, logs, connected } = useWebSocket()
  const { stats, loading } = useStats()

  const aliveCount = agents.filter(a => a.is_alive).length

  return (
    <div className="h-screen flex flex-col bg-[#0a0a0a] text-neutral-300 font-mono text-xs overflow-hidden">
      {/* Header */}
      <header className="flex items-center gap-4 px-5 py-2 border-b border-neutral-800 shrink-0">
        <h1 className="text-sm font-normal tracking-[0.2em] text-neutral-400">ZHI</h1>
        <span className="text-neutral-600">栀 · Cosmos V2</span>
        <div className="flex items-center gap-3 ml-auto">
          <span className="text-[10px] text-neutral-500">Gen {generation}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">Deaths {totalDeaths}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">Alive {aliveCount}/{agents.length}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">Food {food.length}</span>
          <div className="flex items-center gap-2">
            <span className={`w-1.5 h-1.5 rounded-full ${connected ? 'bg-green-500' : 'bg-red-500'}`} />
            <span className="text-[10px] text-neutral-500">{connected ? 'live' : 'off'}</span>
          </div>
        </div>
      </header>

      {/* Main */}
      <div className="flex-1 flex min-h-0">
        {/* Left: World Map */}
        <div className="flex-1 min-h-0 min-w-0 border-r border-neutral-800">
          <WorldMap agents={agents} food={food} />
        </div>

        {/* Right: Stats + Logs */}
        <aside className="w-96 shrink-0 flex flex-col min-h-0">
          <div className="border-b border-neutral-800 shrink-0">
            <StatsPanel stats={stats} loading={loading} />
          </div>
          <div className="flex-1 min-h-0">
            <LogPanel logs={logs} />
          </div>
        </aside>
      </div>
    </div>
  )
}

export default App
