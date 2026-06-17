import { useState, useEffect, useRef, useCallback } from 'react'
import { useWebSocket } from './hooks/useWebSocket'
import { useLogSocket } from './hooks/useLogSocket'
import { useStats } from './hooks/useStats'
import { useEcoHistory } from './hooks/useEcoHistory'
import { WorldMap } from './components/WorldMap'
import { AgentCardsPanel } from './components/AgentCardsPanel'
import { LogPanel } from './components/LogPanel'
import { ChartsPanel } from './components/ChartsPanel'
import { EventMonitor } from './components/EventMonitor'
import { SettingsPanel } from './components/SettingsPanel'
import { LangToggle } from './components/LangToggle'
import { I18nProvider, useT } from './i18n/I18nContext'
import { version } from '../package.json'

function formatGameTime(hours: number): string {
  const h = Math.floor(hours)
  const m = Math.floor((hours - h) * 60)
  const hh = h.toString().padStart(2, '0')
  const mm = m.toString().padStart(2, '0')
  const icon = hours >= 6 && hours < 20 ? '☀' : '🌙'
  return `${icon} ${hh}:${mm}`
}

function tempColor(t: number): string {
  if (t < 5) return '#c0d0ff'
  if (t < 15) return '#60a5fa'
  if (t < 25) return '#4ade80'
  if (t < 35) return '#fb923c'
  return '#ef4444'
}

function AppInner() {
  const { generation, totalDeaths, worldDay, timeOfDay, temperature, gridW, gridH, agents, food, corpses, river, scent, foodScent, temperatureGrid, signalField, terrain, stats, connected } = useWebSocket()
  const { logs, events, clearEvents } = useLogSocket()
  const { stats: dbStats, loading } = useStats()
  const { history, record } = useEcoHistory()
  const { t } = useT()
  const [bottomTab, setBottomTab] = useState<'log' | 'charts' | 'settings'>('log')
  const [trackedAgent, setTrackedAgent] = useState<number | null>(null)
  const [trackNextGen, setTrackNextGen] = useState(false)
  const [bottomHeight, setBottomHeight] = useState(192)
  const resizeRef = useRef<{ startY: number; startH: number } | null>(null)

  const [showScent, setShowScent] = useState(true)
  const [showFoodScent, setShowFoodScent] = useState(true)
  const [showDirection, setShowDirection] = useState(true)
  const [showVision, setShowVision] = useState(true)
  const [showSignal, setShowSignal] = useState(false)
  const [showTemp, setShowTemp] = useState(false)
  const [showTerrain, setShowTerrain] = useState(false)

  const aliveCount = agents.filter(a => a.is_alive).length

  useEffect(() => {
    if (agents.length > 0) {
      record(agents, food, corpses, generation)
    }
  }, [agents, food, corpses, generation, record])

  useEffect(() => {
    if (trackedAgent !== null && !trackNextGen) {
      const agent = agents.find(a => a.id === trackedAgent)
      if (agent && !agent.is_alive) setTrackedAgent(null)
    }
  }, [agents, trackedAgent, trackNextGen])

  const onResizeStart = useCallback((e: React.MouseEvent) => {
    e.preventDefault()
    resizeRef.current = { startY: e.clientY, startH: bottomHeight }
  }, [bottomHeight])

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!resizeRef.current) return
      const dy = resizeRef.current.startY - e.clientY
      const newH = Math.max(80, Math.min(600, resizeRef.current.startH + dy))
      setBottomHeight(newH)
    }
    const onUp = () => { resizeRef.current = null }
    window.addEventListener('mousemove', onMove)
    window.addEventListener('mouseup', onUp)
    return () => {
      window.removeEventListener('mousemove', onMove)
      window.removeEventListener('mouseup', onUp)
    }
  }, [])

  return (
    <div className="h-screen flex flex-col bg-[#0a0a0a] text-neutral-300 font-mono text-xs overflow-hidden">
      {/* Header */}
      <header className="flex items-center gap-4 px-5 py-2 border-b border-neutral-800 shrink-0">
        <h1 className="text-sm font-normal tracking-[0.2em] text-neutral-400">ZHI</h1>
        <span className="text-neutral-600">{t('header.subtitle', { version })}</span>
        <div className="flex items-center gap-3 ml-auto">
          <span className="text-[10px] text-neutral-500">{t('header.gen')} {generation}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">{t('header.deaths')} {totalDeaths}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">{t('header.alive')} {aliveCount}/{agents.length}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">{t('header.food')} {food.length}</span>
          <span className="text-[10px] text-neutral-500">|</span>
          <span className="text-[10px] text-neutral-500">{t('header.corpses')} {corpses.length}</span>
          {!loading && dbStats && (
            <>
              <span className="text-[10px] text-neutral-500">|</span>
              <span className="text-[10px] text-neutral-500">
                {t('header.avgLife')} {dbStats.avg_alive_seconds_recent_10.toFixed(0)}s
              </span>
              <span className="text-[10px] text-neutral-500">|</span>
              <span className="text-[10px] text-neutral-400">
                {t('header.night')} {((dbStats.night_death_rate ?? 0) * 100).toFixed(0)}%
              </span>
              <span className="text-[10px] text-neutral-500">|</span>
              <span className="text-[10px] text-neutral-500">
                {t('header.atk')} {dbStats.avg_attacks_per_life?.toFixed(1) ?? '0'}
              </span>
              <span className="text-[10px] text-neutral-500">
                {t('header.eat')} {dbStats.avg_eats_per_life?.toFixed(1) ?? '0'}
              </span>
              <span className="text-[10px] text-neutral-500">
                {t('header.sig')} {dbStats.avg_signals_per_life?.toFixed(1) ?? '0'}
              </span>
            </>
          )}
          <div className="flex items-center gap-2">
            <LangToggle />
            <span className={`w-1.5 h-1.5 rounded-full ${connected ? 'bg-green-500' : 'bg-red-500'}`} />
            <span className="text-[10px] text-neutral-500">{connected ? t('header.live') : t('header.off')}</span>
          </div>
        </div>
      </header>

      {/* Main */}
      <div className="flex-1 flex min-h-0">
        <aside className="w-64 shrink-0 border-r border-neutral-800 flex flex-col min-h-0">
          <EventMonitor events={events} energySource={stats?.energy_source} onClear={clearEvents} />
        </aside>

        <div className="flex-1 flex flex-col min-h-0 min-w-0">
          {/* Display toggles bar */}
          <div className="flex items-center gap-1 px-3 py-1 border-b border-neutral-800 shrink-0">
            <span className="text-neutral-500 text-[9px]">{t('header.day')} {worldDay}</span>
            <span className="text-neutral-700 text-[9px]">|</span>
            <span className="text-neutral-400 text-[9px]">{formatGameTime(timeOfDay)}</span>
            <span className="text-[9px] font-semibold" style={{ color: tempColor(temperature) }}>{temperature.toFixed(1)}°C</span>
            <span className="text-neutral-700 text-[9px] mx-1">|</span>
            <span className="text-neutral-600 text-[9px] mr-1">{t('toggle.display')}</span>
            <button
              onClick={() => setShowScent(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showScent ? 'border-purple-600 text-purple-400 bg-purple-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              {t('toggle.scent')}
            </button>
            <button
              onClick={() => setShowFoodScent(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showFoodScent ? 'border-green-600 text-green-400 bg-green-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              {t('toggle.foodScent')}
            </button>
            <button
              onClick={() => setShowDirection(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showDirection ? 'border-yellow-600 text-yellow-400 bg-yellow-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              {t('toggle.direction')}
            </button>
            <button
              onClick={() => setShowVision(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showVision ? 'border-purple-600 text-purple-400 bg-purple-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              {t('toggle.vision')}
            </button>
            <button
              onClick={() => setShowSignal(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showSignal ? 'border-yellow-600 text-yellow-400 bg-yellow-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              {t('toggle.signal')}
            </button>
            <button
              onClick={() => setShowTemp(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showTemp ? 'border-orange-600 text-orange-400 bg-orange-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              {t('toggle.temp')}
            </button>
            <button
              onClick={() => setShowTerrain(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${showTerrain ? 'border-amber-600 text-amber-400 bg-amber-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
            >
              {t('toggle.terrain')}
            </button>
            <span className="text-neutral-700 text-[9px] mx-1">|</span>
            <button
              onClick={() => setTrackNextGen(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${trackNextGen ? 'border-cyan-600 text-cyan-400 bg-cyan-900/20' : 'border-neutral-800 text-neutral-600 hover:text-neutral-400'}`}
              title={t('toggle.trackRebirthTitle')}
            >
              {t('toggle.trackRebirth')}
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
              foodScent={foodScent}
              gridW={gridW}
              gridH={gridH}
              timeOfDay={timeOfDay}
              showDirection={showDirection}
              showVision={showVision}
              showSignal={showSignal}
              temperatureGrid={temperatureGrid}
              showTemp={showTemp}
              terrain={terrain}
              showTerrain={showTerrain}
            />
          </div>
          <div className="shrink-0 border-r border-neutral-800 flex flex-col overflow-hidden" style={{ height: bottomHeight }}>
            <div
              className="h-1 shrink-0 bg-neutral-800 hover:bg-neutral-600 cursor-ns-resize transition-colors"
              onMouseDown={onResizeStart}
            />
            <div className="flex items-center gap-1 px-3 py-1 border-b border-neutral-800 shrink-0">
              {(['log', 'charts', 'settings'] as const).map(tab => (
                <button
                  key={tab}
                  className={`px-2 py-0.5 text-[10px] rounded ${bottomTab === tab ? 'bg-neutral-800 text-neutral-300' : 'text-neutral-600 hover:text-neutral-400'}`}
                  onClick={() => setBottomTab(tab)}
                >
                  {tab === 'log' ? t('tab.log') : tab === 'charts' ? t('tab.charts') : t('tab.settings')}
                </button>
              ))}
            </div>
            {bottomTab === 'charts' ? <ChartsPanel data={history} />
              : bottomTab === 'settings' ? <SettingsPanel />
              : <LogPanel logs={logs} />}
          </div>
        </div>

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

function App() {
  return (
    <I18nProvider>
      <AppInner />
    </I18nProvider>
  )
}

export default App
