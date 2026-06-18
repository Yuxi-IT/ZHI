import { useState, useEffect, useRef, useCallback } from 'react';
import { useWebSocket } from '../hooks/useWebSocket';
import { useLogSocket } from '../hooks/useLogSocket';
import { useStats } from '../hooks/useStats';
import { useEcoHistory } from '../hooks/useEcoHistory';
import { WorldMap } from '../components/WorldMap';
import { AgentCardsPanel } from '../components/AgentCardsPanel';
import { LogPanel } from '../components/LogPanel';
import { ChartsPanel } from '../components/ChartsPanel';
import { EventMonitor } from '../components/EventMonitor';
import { SettingsPanel } from '../components/SettingsPanel';
import { ControlBar } from './ControlBar';
import { useT } from '../i18n/I18nContext';

interface Props {
  worldName: string;
  onStop: () => void;
}

function formatGameTime(hours: number): string {
  const h = Math.floor(hours);
  const m = Math.floor((hours - h) * 60);
  const hh = h.toString().padStart(2, '0');
  const mm = m.toString().padStart(2, '0');
  const icon = hours >= 6 && hours < 20 ? '☀' : '🌙';
  return `${icon} ${hh}:${mm}`;
}

function tempColor(t: number): string {
  if (t < 5) return '#c0d0ff';
  if (t < 15) return '#60a5fa';
  if (t < 25) return '#4ade80';
  if (t < 35) return '#fb923c';
  return '#ef4444';
}

export function GameDashboard({ worldName, onStop }: Props) {
  const { generation, totalDeaths, worldDay, timeOfDay, temperature, gridW, gridH, agents, food, corpses, river, scent, foodScent, temperatureGrid, signalField, terrain, terrainTtl, riverFlow, stats, connected } = useWebSocket();
  const { logs, events, clearEvents } = useLogSocket();
  const { stats: dbStats, loading } = useStats();
  const { history, record } = useEcoHistory();
  const { t } = useT();

  const [bottomTab, setBottomTab] = useState<'log' | 'charts' | 'settings'>('log');
  const [trackedAgent, setTrackedAgent] = useState<number | null>(null);
  const [trackNextGen, setTrackNextGen] = useState(false);
  const [bottomHeight, setBottomHeight] = useState(192);
  const [paused, setPaused] = useState(false);
  const [stopping, setStopping] = useState(false);
  const resizeRef = useRef<{ startY: number; startH: number } | null>(null);

  const [showScent, setShowScent] = useState(true);
  const [showFoodScent, setShowFoodScent] = useState(true);
  const [showDirection, setShowDirection] = useState(true);
  const [showVision, setShowVision] = useState(true);
  const [showSignal, setShowSignal] = useState(false);
  const [showTemp, setShowTemp] = useState(false);
  const [showTerrain, setShowTerrain] = useState(false);
  const [showFlow, setShowFlow] = useState(false);

  const aliveCount = agents.filter(a => a.is_alive).length;

  useEffect(() => {
    if (agents.length > 0) {
      record(agents, food, corpses, generation);
    }
  }, [agents, food, corpses, generation, record]);

  useEffect(() => {
    if (trackedAgent !== null && !trackNextGen) {
      const agent = agents.find(a => a.id === trackedAgent);
      if (agent && !agent.is_alive) setTrackedAgent(null);
    }
  }, [agents, trackedAgent, trackNextGen]);

  const onResizeStart = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    resizeRef.current = { startY: e.clientY, startH: bottomHeight };
  }, [bottomHeight]);

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!resizeRef.current) return;
      const dy = resizeRef.current.startY - e.clientY;
      const newH = Math.max(80, Math.min(600, resizeRef.current.startH + dy));
      setBottomHeight(newH);
    };
    const onUp = () => { resizeRef.current = null; };
    window.addEventListener('mousemove', onMove);
    window.addEventListener('mouseup', onUp);
    return () => {
      window.removeEventListener('mousemove', onMove);
      window.removeEventListener('mouseup', onUp);
    };
  }, []);

  const handlePause = useCallback(async () => {
    try {
      await fetch('/api/pause', { method: 'POST' });
      setPaused(v => !v);
    } catch (err) {
      console.error('Failed to toggle pause:', err);
    }
  }, []);

  const handleStop = useCallback(async () => {
    setStopping(true);
    try {
      await fetch('/api/stop', { method: 'POST' });
      onStop();
    } catch (err) {
      console.error('Failed to stop world:', err);
      setStopping(false);
    }
  }, [onStop]);

  return (
    <div className="h-screen flex flex-col bg-zhi-bg text-zhi-text font-mono text-xs overflow-hidden">
      <ControlBar
        worldName={worldName}
        generation={generation}
        totalDeaths={totalDeaths}
        aliveCount={aliveCount}
        agentCount={agents.length}
        connected={connected}
        onPause={handlePause}
        onStop={handleStop}
        paused={paused}
        stopping={stopping}
      />

      {/* Main */}
      <div className="flex-1 flex min-h-0">
        <aside className="w-64 shrink-0 border-r border-zhi-border flex flex-col min-h-0">
          <EventMonitor events={events} energySource={stats?.energy_source} onClear={clearEvents} />
        </aside>

        <div className="flex-1 flex flex-col min-h-0 min-w-0">
          {/* Display toggles bar */}
          <div className="flex items-center gap-1 px-3 py-1 border-b border-zhi-border shrink-0">
            <span className="text-zhi-muted text-[9px]">{t('header.day')} {worldDay}</span>
            <span className="text-zhi-border text-[9px]">|</span>
            <span className="text-zhi-text text-[9px]">{formatGameTime(timeOfDay)}</span>
            <span className="text-[9px] font-semibold" style={{ color: tempColor(temperature) }}>{temperature.toFixed(1)}°C</span>
            <span className="text-zhi-border text-[9px] mx-1">|</span>
            <span className="text-zhi-muted text-[9px] mr-1">{t('toggle.display')}</span>
            <button onClick={() => setShowScent(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showScent ? 'border-purple-600 text-purple-400 bg-purple-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.scent')}</button>
            <button onClick={() => setShowFoodScent(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showFoodScent ? 'border-green-600 text-green-400 bg-green-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.foodScent')}</button>
            <button onClick={() => setShowDirection(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showDirection ? 'border-yellow-600 text-yellow-400 bg-yellow-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.direction')}</button>
            <button onClick={() => setShowVision(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showVision ? 'border-purple-600 text-purple-400 bg-purple-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.vision')}</button>
            <button onClick={() => setShowSignal(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showSignal ? 'border-yellow-600 text-yellow-400 bg-yellow-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.signal')}</button>
            <button onClick={() => setShowTemp(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showTemp ? 'border-orange-600 text-orange-400 bg-orange-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.temp')}</button>
            <button onClick={() => setShowTerrain(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showTerrain ? 'border-amber-600 text-amber-400 bg-amber-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.terrain')}</button>
            <button onClick={() => setShowFlow(v => !v)} className={`px-1.5 py-0.5 text-[9px] rounded border ${showFlow ? 'border-sky-600 text-sky-400 bg-sky-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}>{t('toggle.flow')}</button>
            <span className="text-zhi-border text-[9px] mx-1">|</span>
            <button
              onClick={() => setTrackNextGen(v => !v)}
              className={`px-1.5 py-0.5 text-[9px] rounded border ${trackNextGen ? 'border-cyan-600 text-cyan-400 bg-cyan-900/20' : 'border-zhi-border text-zhi-muted hover:text-zhi-text'}`}
              title={t('toggle.trackRebirthTitle')}
            >
              {t('toggle.trackRebirth')}
            </button>
            {stats && (
              <>
                <span className="text-zhi-border text-[9px] ml-2">|</span>
                <span className="text-zhi-muted text-[9px]">ATK:{stats.attack_rate.toFixed(2)}/t</span>
              </>
            )}
            {!loading && dbStats && (
              <>
                <span className="text-zhi-border text-[9px]">|</span>
                <span className="text-[9px] text-zhi-muted">
                  {t('header.avgLife')} {dbStats.avg_alive_seconds_recent_10.toFixed(0)}s
                </span>
                <span className="text-[9px] text-zhi-muted">
                  {t('header.night')} {((dbStats.night_death_rate ?? 0) * 100).toFixed(0)}%
                </span>
                <span className="text-[9px] text-zhi-muted">
                  {t('header.atk')} {dbStats.avg_attacks_per_life?.toFixed(1) ?? '0'}
                </span>
                <span className="text-[9px] text-zhi-muted">
                  {t('header.eat')} {dbStats.avg_eats_per_life?.toFixed(1) ?? '0'}
                </span>
                <span className="text-[9px] text-zhi-muted">
                  {t('header.sig')} {dbStats.avg_signals_per_life?.toFixed(1) ?? '0'}
                </span>
              </>
            )}
          </div>

          <div className="flex-1 min-h-0 border-r border-zhi-border">
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
              terrainTtl={terrainTtl}
              showTerrain={showTerrain}
              riverFlow={riverFlow}
              showFlow={showFlow}
            />
          </div>

          {/* Bottom panel */}
          <div className="shrink-0 border-r border-zhi-border flex flex-col overflow-hidden" style={{ height: bottomHeight }}>
            <div
              className="h-1 shrink-0 bg-zhi-border hover:bg-zhi-muted cursor-ns-resize transition-colors"
              onMouseDown={onResizeStart}
            />
            <div className="flex items-center gap-1 px-3 py-1 border-b border-zhi-border shrink-0">
              {(['log', 'charts', 'settings'] as const).map(tab => (
                <button
                  key={tab}
                  className={`px-2 py-0.5 text-[10px] rounded ${bottomTab === tab ? 'bg-zhi-border text-zhi-text' : 'text-zhi-muted hover:text-zhi-text'}`}
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
            terrain={terrain}
            gridW={gridW}
          />
        </aside>
      </div>
    </div>
  );
}
