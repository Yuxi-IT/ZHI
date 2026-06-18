import { useState, useEffect, useRef, useCallback, memo } from 'react';
import { Button, Separator, Tooltip } from '@heroui/react';
import { useWebSocket } from '../hooks/useWebSocket';
import { useLogSocket } from '../hooks/useLogSocket';
import { useStats } from '../hooks/useStats';
import { useEcoHistory } from '../hooks/useEcoHistory';
import { WorldMap } from '../components/WorldMap';
import type { DrawData } from '../components/WorldMap';
import { WorldMap3D } from '../components/WorldMap3D';
import { AgentCardsPanel } from '../components/AgentCardsPanel';
import { LogPanel } from '../components/LogPanel';
import { ChartsPanel } from '../components/ChartsPanel';
import { EventMonitor } from '../components/EventMonitor';
import { ControlBar } from './ControlBar';
import { useT } from '../i18n/I18nContext';

interface Props {
  worldName: string;
  onStop: () => void;
}

function formatWorldTime(hours: number): string {
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

export function WorldDashboard({ worldName, onStop }: Props) {
  const { generation, totalDeaths, worldDay, timeOfDay, temperature, gridW, gridH, agents, food, corpses, river, scent, foodScent, temperatureGrid, chemicalField, heightMap, slope, riverFlow, surfaceWater, groundwater, nutrient, permeability, pressure, windX, windY, sunlight, biome, waterCycle, plantCount, stats, connected } = useWebSocket();
  const { logs, events, clearEvents } = useLogSocket();
  const { stats: dbStats, loading } = useStats();
  const { history, record } = useEcoHistory();
  const { t } = useT();

  const [bottomTab, setBottomTab] = useState<'log' | 'charts'>('log');
  const [trackedAgent, setTrackedAgent] = useState<number | null>(null);
  const [trackNextGen, setTrackNextGen] = useState(false);
  const [bottomHeight, setBottomHeight] = useState(192);
  const [paused, setPaused] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [speed, setSpeed] = useState(1);
  const [leftWidth, setLeftWidth] = useState(256);
  const [rightWidth, setRightWidth] = useState(320);
  const resizeRef = useRef<{ side: 'bottom' | 'left' | 'right'; startPos: number; startSize: number } | null>(null);

  const [showScent, setShowScent] = useState(true);
  const [showFoodScent, setShowFoodScent] = useState(true);
  const [showDirection, setShowDirection] = useState(true);
  const [showVision, setShowVision] = useState(true);
  const [showChemical, setShowChemical] = useState(false);
  const [showTemp, setShowTemp] = useState(false);
  const [showTerrain, setShowTerrain] = useState(false);
  const [showFlow, setShowFlow] = useState(false);
  const [showGroundwater, setShowGroundwater] = useState(false);
  const [showSurfaceWater, setShowSurfaceWater] = useState(false);
  const [showNutrient, setShowNutrient] = useState(false);
  const [showPermeability, setShowPermeability] = useState(false);
  const [showPressure, setShowPressure] = useState(false);
  const [showWind, setShowWind] = useState(false);
  const [showBiome, setShowBiome] = useState(false);
  const [use3D, setUse3D] = useState(false);

  const aliveCount = agents.filter(a => a.is_alive).length;

  const drawDataRef = useRef<DrawData>({
    agents: [], food: [], corpses: [], river: [], scent: [], foodScent: [],
    chemicalField: [], temperatureGrid: [], heightMap: [], slope: [], riverFlow: [],
    surfaceWater: [], groundwater: [], nutrient: [], permeability: [], pressure: [], windX: [], windY: [],
    sunlight: [], biome: [],
    events: [], timeOfDay: 12, trackedAgent: null,
  });
  drawDataRef.current = {
    agents, food, corpses, river, scent, foodScent, chemicalField,
    temperatureGrid, heightMap, slope, riverFlow, surfaceWater, groundwater, nutrient, permeability, pressure, windX, windY,
    sunlight, biome,
    events,
    timeOfDay, trackedAgent,
  };

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

  const onResizeBottom = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    resizeRef.current = { side: 'bottom', startPos: e.clientY, startSize: bottomHeight };
  }, [bottomHeight]);

  const onResizeLeft = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    resizeRef.current = { side: 'left', startPos: e.clientX, startSize: leftWidth };
  }, [leftWidth]);

  const onResizeRight = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    resizeRef.current = { side: 'right', startPos: e.clientX, startSize: rightWidth };
  }, [rightWidth]);

  useEffect(() => {
    const onMove = (e: MouseEvent) => {
      if (!resizeRef.current) return;
      const { side, startPos, startSize } = resizeRef.current;
      if (side === 'bottom') {
        const dy = startPos - e.clientY;
        setBottomHeight(Math.max(80, Math.min(600, startSize + dy)));
      } else if (side === 'left') {
        const dx = e.clientX - startPos;
        setLeftWidth(Math.max(180, Math.min(500, startSize + dx)));
      } else if (side === 'right') {
        const dx = startPos - e.clientX;
        setRightWidth(Math.max(200, Math.min(500, startSize + dx)));
      }
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

  const handleSpeedChange = useCallback(async (multiplier: number) => {
    setSpeed(multiplier);
    try {
      await fetch('/api/speed', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ multiplier }),
      });
    } catch (err) {
      console.error('Failed to set speed:', err);
    }
  }, []);

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
        speed={speed}
        onSpeedChange={handleSpeedChange}
      />

      <div className="flex-1 flex min-h-0">
        <aside className="shrink-0 flex flex-col min-h-0 relative" style={{ width: leftWidth }}>
          <div className="flex-1 min-h-0 border-r border-zhi-border">
            <EventMonitor events={events} energySource={stats?.energy_source} onClear={clearEvents} />
          </div>
          <div
            className="absolute top-0 -right-0.5 w-1.5 h-full cursor-col-resize hover:bg-zhi-muted/30 transition-colors z-10"
            onMouseDown={onResizeLeft}
          />
        </aside>

        <div className="flex-1 flex flex-col min-h-0 min-w-0">
          {/* Info bar — dynamic data */}
          <div className="flex items-center gap-1.5 px-3 py-1 border-b border-zhi-border shrink-0 flex-wrap">
            <MetricTip tip={t('header.dayTip')}>
              <span className="text-zhi-muted text-[11px]">{t('header.day')} {worldDay}</span>
            </MetricTip>
            <Separator orientation="vertical" className="h-3" />
            <MetricTip tip={t('header.timeTip')}>
              <span className="text-zhi-text text-[11px]">{formatWorldTime(timeOfDay)}</span>
            </MetricTip>
            <MetricTip tip={t('header.tempTip')}>
              <span className="text-[11px] font-semibold cursor-help" style={{ color: tempColor(temperature) }}>{temperature.toFixed(1)}°C</span>
            </MetricTip>
            <Separator orientation="vertical" className="h-3" />
            <span className="text-zhi-muted text-[11px]">{t('header.plants')} {plantCount}</span>
            {waterCycle && (
              <>
                <Separator orientation="vertical" className="h-3" />
                <span className="text-zhi-muted text-[11px]">{waterCycle.is_wet_season ? t('header.wetSeason') : t('header.drySeason')} H:{waterCycle.humidity.toFixed(1)}</span>
              </>
            )}
            <Separator orientation="vertical" className="h-3" />
            <span className="text-zhi-muted text-[11px]">{t('toggle.display')}</span>
            {stats && (
              <>
                <Separator orientation="vertical" className="h-3" />
                <MetricTip tip={t('header.atkRateTip')}>
                  <span className="text-zhi-muted text-[11px]">{t('header.atkRate', { rate: stats.attack_rate.toFixed(2) })}</span>
                </MetricTip>
              </>
            )}
            {!loading && dbStats && (
              <>
                <Separator orientation="vertical" className="h-3" />
                <MetricTip tip={t('header.avgLifeTip')}>
                  <span className="text-[11px] text-zhi-muted">{t('header.avgLife')} {dbStats.avg_alive_seconds_recent_10.toFixed(0)}s</span>
                </MetricTip>
                <MetricTip tip={t('header.nightTip')}>
                  <span className="text-[11px] text-zhi-muted">{t('header.night')} {((dbStats.night_death_rate ?? 0) * 100).toFixed(0)}%</span>
                </MetricTip>
                <MetricTip tip={t('header.atkTip')}>
                  <span className="text-[11px] text-zhi-muted">{t('header.atk')} {dbStats.avg_attacks_per_life?.toFixed(1) ?? '0'}</span>
                </MetricTip>
                <MetricTip tip={t('header.eatTip')}>
                  <span className="text-[11px] text-zhi-muted">{t('header.eat')} {dbStats.avg_eats_per_life?.toFixed(1) ?? '0'}</span>
                </MetricTip>
                <MetricTip tip={t('header.sigTip')}>
                  <span className="text-[11px] text-zhi-muted">{t('header.sig')} {dbStats.avg_emits_per_life?.toFixed(1) ?? '0'}</span>
                </MetricTip>
              </>
            )}
          </div>

          {/* Toggle bar — memoized, doesn't re-render on data ticks */}
          <DisplayToggles
            showScent={showScent} setShowScent={setShowScent}
            showFoodScent={showFoodScent} setShowFoodScent={setShowFoodScent}
            showDirection={showDirection} setShowDirection={setShowDirection}
            showVision={showVision} setShowVision={setShowVision}
            showChemical={showChemical} setShowChemical={setShowChemical}
            showTemp={showTemp} setShowTemp={setShowTemp}
            showTerrain={showTerrain} setShowTerrain={setShowTerrain}
            showFlow={showFlow} setShowFlow={setShowFlow}
            showGroundwater={showGroundwater} setShowGroundwater={setShowGroundwater}
            showSurfaceWater={showSurfaceWater} setShowSurfaceWater={setShowSurfaceWater}
            showNutrient={showNutrient} setShowNutrient={setShowNutrient}
            showPermeability={showPermeability} setShowPermeability={setShowPermeability}
            showPressure={showPressure} setShowPressure={setShowPressure}
            showWind={showWind} setShowWind={setShowWind}
            showBiome={showBiome} setShowBiome={setShowBiome}
            use3D={use3D} setUse3D={setUse3D}
            trackNextGen={trackNextGen} setTrackNextGen={setTrackNextGen}
          />

          <div className="flex-1 min-h-0">
            {use3D ? (
              <WorldMap3D
                drawDataRef={drawDataRef}
                gridW={gridW}
                gridH={gridH}
                trackedAgent={trackedAgent}
                showBiome={showBiome}
              />
            ) : (
              <WorldMap
                drawDataRef={drawDataRef}
                gridW={gridW}
                gridH={gridH}
                trackedAgent={trackedAgent}
                onTrackChange={setTrackedAgent}
                showScent={showScent}
                showFoodScent={showFoodScent}
                showDirection={showDirection}
                showVision={showVision}
                showChemical={showChemical}
                showTemp={showTemp}
                showTerrain={showTerrain}
                showFlow={showFlow}
                showGroundwater={showGroundwater}
                showSurfaceWater={showSurfaceWater}
                showNutrient={showNutrient}
                showPermeability={showPermeability}
                showPressure={showPressure}
                showWind={showWind}
                showBiome={showBiome}
              />
            )}
          </div>

          {/* Bottom panel */}
          <div className="shrink-0 border-r border-zhi-border flex flex-col overflow-hidden" style={{ height: bottomHeight }}>
            <div
              className="h-1 shrink-0 bg-zhi-border hover:bg-zhi-muted cursor-ns-resize transition-colors"
              onMouseDown={onResizeBottom}
            />
            <div className="flex items-center gap-1 px-3 py-1 border-b border-zhi-border shrink-0">
              {(['log', 'charts'] as const).map(tab => (
                <Button
                  key={tab}
                  variant={bottomTab === tab ? 'secondary' : 'ghost'}
                  size="sm"
                  className={`text-[10px] min-w-0 h-auto px-2 py-0.5 ${bottomTab === tab ? 'bg-zhi-border text-zhi-text' : 'text-zhi-muted hover:text-zhi-text'}`}
                  onPress={() => setBottomTab(tab)}
                >
                  {tab === 'log' ? t('tab.log') : t('tab.charts')}
                </Button>
              ))}
            </div>
            {bottomTab === 'charts' ? <ChartsPanel data={history} />
              : <LogPanel logs={logs} />}
          </div>
        </div>

        <aside className="shrink-0 flex flex-col min-h-0 relative" style={{ width: rightWidth }}>
          <div
            className="absolute top-0 -left-0.5 w-1.5 h-full cursor-col-resize hover:bg-zhi-muted/30 transition-colors z-10"
            onMouseDown={onResizeRight}
          />
          <div className="flex-1 min-h-0 border-l border-zhi-border">
            <AgentCardsPanel
              agents={agents}
              trackedId={trackedAgent}
              onTrack={setTrackedAgent}
              gridW={gridW}
            />
          </div>
        </aside>
      </div>
    </div>
  );
}

function MetricTip({ tip, children }: { tip: string; children: React.ReactNode }) {
  return (
    <Tooltip delay={300}>
      {children}
      <Tooltip.Content>
        <p className="text-xs max-w-52">{tip}</p>
      </Tooltip.Content>
    </Tooltip>
  );
}

type ToggleKeys = {
  showScent: boolean; setShowScent: (v: boolean) => void;
  showFoodScent: boolean; setShowFoodScent: (v: boolean) => void;
  showDirection: boolean; setShowDirection: (v: boolean) => void;
  showVision: boolean; setShowVision: (v: boolean) => void;
  showChemical: boolean; setShowChemical: (v: boolean) => void;
  showTemp: boolean; setShowTemp: (v: boolean) => void;
  showTerrain: boolean; setShowTerrain: (v: boolean) => void;
  showFlow: boolean; setShowFlow: (v: boolean) => void;
  showGroundwater: boolean; setShowGroundwater: (v: boolean) => void;
  showSurfaceWater: boolean; setShowSurfaceWater: (v: boolean) => void;
  showNutrient: boolean; setShowNutrient: (v: boolean) => void;
  showPermeability: boolean; setShowPermeability: (v: boolean) => void;
  showPressure: boolean; setShowPressure: (v: boolean) => void;
  showWind: boolean; setShowWind: (v: boolean) => void;
  showBiome: boolean; setShowBiome: (v: boolean) => void;
  use3D: boolean; setUse3D: (v: boolean) => void;
  trackNextGen: boolean; setTrackNextGen: (v: boolean) => void;
};

const DisplayToggles = memo(function DisplayToggles(p: ToggleKeys) {
  const { t } = useT();
  const btns: [boolean, (v: boolean) => void, string, string][] = [
    [p.showScent, p.setShowScent, t('toggle.scent'), 'border-purple-600 text-purple-400 bg-purple-900/20'],
    [p.showFoodScent, p.setShowFoodScent, t('toggle.foodScent'), 'border-green-600 text-green-400 bg-green-900/20'],
    [p.showDirection, p.setShowDirection, t('toggle.direction'), 'border-yellow-600 text-yellow-400 bg-yellow-900/20'],
    [p.showVision, p.setShowVision, t('toggle.vision'), 'border-purple-600 text-purple-400 bg-purple-900/20'],
    [p.showChemical, p.setShowChemical, t('toggle.chemical'), 'border-yellow-600 text-yellow-400 bg-yellow-900/20'],
    [p.showTemp, p.setShowTemp, t('toggle.temp'), 'border-orange-600 text-orange-400 bg-orange-900/20'],
    [p.showTerrain, p.setShowTerrain, t('toggle.terrain'), 'border-amber-600 text-amber-400 bg-amber-900/20'],
    [p.showFlow, p.setShowFlow, t('toggle.flow'), 'border-sky-600 text-sky-400 bg-sky-900/20'],
    [p.showGroundwater, p.setShowGroundwater, t('toggle.groundwater'), 'border-blue-600 text-blue-400 bg-blue-900/20'],
    [p.showSurfaceWater, p.setShowSurfaceWater, t('toggle.surfaceWater'), 'border-cyan-600 text-cyan-400 bg-cyan-900/20'],
    [p.showNutrient, p.setShowNutrient, t('toggle.nutrient'), 'border-amber-600 text-amber-400 bg-amber-900/20'],
    [p.showPermeability, p.setShowPermeability, t('toggle.permeability'), 'border-teal-600 text-teal-400 bg-teal-900/20'],
    [p.showPressure, p.setShowPressure, t('toggle.pressure'), 'border-red-600 text-red-400 bg-red-900/20'],
    [p.showWind, p.setShowWind, t('toggle.wind'), 'border-slate-400 text-slate-300 bg-slate-700/30'],
    [p.showBiome, p.setShowBiome, t('toggle.biome'), 'border-lime-600 text-lime-400 bg-lime-900/20'],
    [p.use3D, p.setUse3D, t('toggle.3d'), 'border-indigo-500 text-indigo-300 bg-indigo-900/30'],
    [p.trackNextGen, p.setTrackNextGen, t('toggle.trackRebirth'), 'border-cyan-600 text-cyan-400 bg-cyan-900/20'],
  ];

  return (
    <div className="flex items-center gap-1.5 px-3 py-1 border-b border-zhi-border shrink-0 flex-wrap">
      {btns.map(([on, set, label, cls]) => (
        <Button
          key={label}
          variant={on ? 'secondary' : 'ghost'}
          size="sm"
          onPress={() => set(!on)}
          className={`text-[10px] min-w-0 h-auto px-1.5 py-0.5 rounded border transition-colors ${
            on ? cls : 'border-zhi-border text-zhi-muted hover:text-zhi-text'
          }`}
        >
          {label}
        </Button>
      ))}
    </div>
  );
});
