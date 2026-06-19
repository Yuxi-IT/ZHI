import { useRef, useEffect, useState, memo } from 'react';
import type { AgentSnapshot, FoodTile, CorpseTile, WorldEvent } from '../types';
import { useT } from '../i18n/I18nContext';
import {
  drawBackground, drawGridBg, drawGridLines,
  drawRiver, drawHeightMap, drawRiverFlow,
  drawFoodScent, drawAgentScent, drawChemical,
  drawTemperature, drawSurfaceWater, drawGroundwater,
  drawNutrient, drawPermeability, drawPressure, drawWind,
  drawBiome, drawCorpses, drawPlants, drawVision,
  drawAgents, drawFloatingTexts, drawHud,
} from './WorldMapLayers';

const MIN_ZOOM = 0.1;
const MAX_ZOOM = 12;

export interface DrawData {
  agents: AgentSnapshot[];
  food: FoodTile[];
  corpses: CorpseTile[];
  river: number[];
  scent: number[];
  foodScent: number[];
  chemicalField: number[];
  temperatureGrid: number[];
  heightMap: number[];
  slope: number[];
  riverFlow: number[];
  surfaceWater: number[];
  groundwater: number[];
  nutrient: number[];
  permeability: number[];
  pressure: number[];
  windX: number[];
  windY: number[];
  sunlight: number[];
  biome: number[];
  events: WorldEvent[];
  timeOfDay: number;
  trackedAgent: number | null;
}

interface Props {
  drawDataRef: { current: DrawData };
  gridW?: number;
  gridH?: number;
  trackedAgent?: number | null;
  onTrackChange?: (id: number | null) => void;
  showScent?: boolean;
  showFoodScent?: boolean;
  showDirection?: boolean;
  showVision?: boolean;
  showChemical?: boolean;
  showTemp?: boolean;
  showTerrain?: boolean;
  showFlow?: boolean;
  showGroundwater?: boolean;
  showSurfaceWater?: boolean;
  showNutrient?: boolean;
  showPermeability?: boolean;
  showPressure?: boolean;
  showWind?: boolean;
  showBiome?: boolean;
}

interface TooltipInfo {
  text: string[];
  x: number;
  y: number;
}

interface FloatingText {
  id: number;
  x: number;
  y: number;
  text: string;
  color: string;
  startTime: number;
}

export const WorldMap = memo(function WorldMap({
  drawDataRef,
  gridW = 64, gridH = 64,
  trackedAgent: trackedProp, onTrackChange,
  showScent = false, showFoodScent = false,
  showDirection = false, showVision = false,
  showChemical = false, showTemp = false,
  showTerrain = false, showFlow = false,
  showGroundwater = false, showSurfaceWater = false, showNutrient = false,
  showPermeability = false,
  showPressure = false,
  showWind = false,
  showBiome = false,
}: Props) {
  const { t } = useT();
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const camRef = useRef({ x: 0, y: 0, zoom: 1 });
  const dragRef = useRef({ dragging: false, lastX: 0, lastY: 0 });
  const [internalTracked, setInternalTracked] = useState<number | null>(null);
  const [tooltip, setTooltip] = useState<TooltipInfo | null>(null);
  const rafRef = useRef<number>(0);
  const drawRef = useRef<() => void>(() => {});
  const floatingTextsRef = useRef<FloatingText[]>([]);
  const floatingIdRef = useRef(0);
  const lastEidRef = useRef(-1);

  const trackedAgent = trackedProp !== undefined ? trackedProp : internalTracked;
  const setTrackedAgent = onTrackChange ?? setInternalTracked;

  // Build draw function — updates when layer visibility or grid size changes
  useEffect(() => {
    drawRef.current = () => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;

      const data = drawDataRef.current;
      const tracked = trackedProp !== undefined ? trackedProp : data.trackedAgent;

      // Process new events → floating texts
      if (data.events && data.events.length > 0) {
        const newEvents: typeof data.events = [];
        for (let i = data.events.length - 1; i >= 0; i--) {
          const eid = (data.events[i]! as any)._eid as number | undefined;
          if (eid === undefined || eid <= lastEidRef.current) break;
          newEvents.unshift(data.events[i]!);
        }
        if (newEvents.length > 0) {
          lastEidRef.current = (data.events[data.events.length - 1] as any)._eid ?? lastEidRef.current;
          const now = performance.now();
          for (const ev of newEvents) {
            const agent = data.agents.find(a => a.id === ev.agent_id);
            if (ev.type === 'attack' && ev.target_id !== undefined) {
              const target = data.agents.find(a => a.id === ev.target_id);
              const tx = target?.x ?? agent?.x ?? 0;
              const ty = target?.y ?? agent?.y ?? 0;
              floatingTextsRef.current.push({
                id: floatingIdRef.current++, x: tx, y: ty,
                text: `-${ev.value.toFixed(0)}`, color: '#ef4444', startTime: now,
              });
              continue;
            }
            if (!agent) continue;
            let text = '', color = '';
            if (ev.type === 'eat') { text = `+${ev.value.toFixed(0)}`; color = '#22c55e'; }
            else if (ev.type === 'death') { text = 'DEAD'; color = '#94a3b8'; }
            else if (ev.type === 'respawn') { text = 'RESPAWN'; color = '#a78bfa'; }
            else continue;
            floatingTextsRef.current.push({
              id: floatingIdRef.current++, x: agent.x, y: agent.y,
              text, color, startTime: now,
            });
          }
        }
      }

      const rect = canvas.getBoundingClientRect();
      const dpr = window.devicePixelRatio || 1;
      canvas.width = rect.width * dpr;
      canvas.height = rect.height * dpr;
      ctx.scale(dpr, dpr);

      const w = rect.width, h = rect.height;
      const cam = camRef.current;
      const cellSize = cam.zoom * (w / gridW);

      // Tracked agent — center camera
      if (tracked !== null) {
        const agent = data.agents.find(a => a.id === tracked && a.is_alive);
        if (agent) {
          cam.x = agent.x * cellSize + cellSize / 2 - w / 2;
          cam.y = agent.y * cellSize + cellSize / 2 - h / 2;
        }
      }

      const brightness = (Math.cos((data.timeOfDay - 14) * Math.PI / 12) + 1) / 2;
      const l = { ctx, w, h, camX: cam.x, camY: cam.y, cellSize, brightness };

      drawBackground(l);

      ctx.save();
      ctx.translate(-cam.x, -cam.y);

      drawGridBg(l, gridW, gridH);
      drawGridLines(l, gridW, gridH);

      // River & terrain
      if (data.river.length > 0) drawRiver(l, data.river, gridW, gridH);
      if (showTerrain && data.heightMap?.length) drawHeightMap(l, data.heightMap, gridW, gridH);
      if (showFlow && data.riverFlow?.length) drawRiverFlow(l, data.riverFlow, gridW, gridH);

      // Scents & signals
      if (showFoodScent && data.foodScent?.length) drawFoodScent(l, data.foodScent, gridW, gridH);
      if (showScent && data.scent?.length) drawAgentScent(l, data.scent, gridW, gridH);
      if (showChemical && data.chemicalField?.length) drawChemical(l, data.chemicalField, gridW, gridH);

      // Environmental overlays
      if (showTemp && data.temperatureGrid?.length) drawTemperature(l, data.temperatureGrid, gridW, gridH);
      if (showSurfaceWater && data.surfaceWater?.length) drawSurfaceWater(l, data.surfaceWater, gridW, gridH);
      if (showGroundwater && data.groundwater?.length) drawGroundwater(l, data.groundwater, gridW, gridH);
      if (showNutrient && data.nutrient?.length) drawNutrient(l, data.nutrient, gridW, gridH);
      if (showPermeability && data.permeability?.length) drawPermeability(l, data.permeability, gridW, gridH);
      if (showPressure && data.pressure?.length) drawPressure(l, data.pressure, gridW, gridH);
      if (showWind && data.windX?.length) drawWind(l, data.windX, data.windY, gridW, gridH);
      if (showBiome && data.biome?.length) drawBiome(l, data.biome, gridW, gridH);

      // Entities
      drawCorpses(l, data.corpses);
      drawPlants(l, data.food);
      if (showVision) drawVision(l, data.agents, data.heightMap, gridW, gridH);
      drawAgents(l, data.agents, tracked, showDirection, t('map.zzz'), cellSize);
      drawFloatingTexts(l, floatingTextsRef.current);

      ctx.restore();

      // HUD
      const zoomPct = Math.round(cam.zoom * 100);
      const alive = data.agents.filter(a => a.is_alive).length;
      const hudLabel = tracked !== null
        ? t('map.hudTracking', { zoom: zoomPct, id: tracked, alive, total: data.agents.length })
        : t('map.hudNormal', { zoom: zoomPct, alive, total: data.agents.length });
      drawHud(l, data.agents, zoomPct, tracked, hudLabel);
    };
  }, [showScent, showFoodScent, showDirection, showVision, showChemical, showTemp, showTerrain, showFlow, showGroundwater, showSurfaceWater, showNutrient, showPermeability, showPressure, showWind, showBiome, gridW, gridH, t, trackedProp, drawDataRef]);

  // Stable rAF loop
  useEffect(() => {
    let animating = true;
    const loop = () => {
      if (!animating) return;
      drawRef.current();
      rafRef.current = requestAnimationFrame(loop);
    };
    rafRef.current = requestAnimationFrame(loop);
    return () => { animating = false; cancelAnimationFrame(rafRef.current); };
  }, []);

  // Mouse events
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const onWheel = (e: WheelEvent) => {
      e.preventDefault();
      const cam = camRef.current;
      const rect = canvas.getBoundingClientRect();
      const mx = e.clientX - rect.left, my = e.clientY - rect.top;
      const oldZoom = cam.zoom;
      cam.zoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, oldZoom * (e.deltaY < 0 ? 1.15 : 1 / 1.15)));
      cam.x = (cam.x + mx) * (cam.zoom / oldZoom) - mx;
      cam.y = (cam.y + my) * (cam.zoom / oldZoom) - my;
      setTrackedAgent(null);
    };

    const onMouseDown = (e: MouseEvent) => {
      if (e.button === 0) dragRef.current = { dragging: true, lastX: e.clientX, lastY: e.clientY };
    };

    const onMouseMove = (e: MouseEvent) => {
      const rect = canvas.getBoundingClientRect();
      const mx = e.clientX - rect.left, my = e.clientY - rect.top;

      if (dragRef.current.dragging) {
        camRef.current.x -= e.clientX - dragRef.current.lastX;
        camRef.current.y -= e.clientY - dragRef.current.lastY;
        dragRef.current.lastX = e.clientX;
        dragRef.current.lastY = e.clientY;
        setTrackedAgent(null);
      } else {
        const data = drawDataRef.current;
        const cam = camRef.current;
        const cellSize = cam.zoom * (rect.width / gridW);
        const gx = Math.floor((cam.x + mx) / cellSize);
        const gy = Math.floor((cam.y + my) / cellSize);
        if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) { setTooltip(null); return; }

        const lines: string[] = [];
        const agent = data.agents.find(a => a.is_alive && a.x === gx && a.y === gy);
        if (agent) {
          lines.push(
            t('map.tooltipAgent', { id: agent.id }) + (agent.respawn_count > 0 ? ` ${t('map.tooltipGen', { gen: agent.respawn_count })}` : ''),
            `${t('map.tooltipEnergy')}: ${agent.energy.toFixed(1)}  ${t('agents.stress')}: ${agent.stress.toFixed(2)}`,
            `${t('agents.water')}: ${agent.water.toFixed(1)}  ${t('map.tooltipBTemp')}: ${agent.body_temperature.toFixed(1)}°C  ${t('agents.age')}: ${agent.tick_count}`,
            `${t('agents.action')}: ${agent.is_eating ? '\u{1F356} ' : ''}${agent.last_action || t('agents.none')}`,
            `${t('map.tooltipEats')}: ${agent.eat_count}  ${t('map.tooltipAttacks')}: ${agent.attack_count}  ${t('map.tooltipEmits')}: ${agent.emit_count}`,
          );
        }
        if (data.river.length > 0) {
          const rv = data.river[gy * gridW + gx];
          if (rv === 1) { lines.push(t('map.shallow'), t('map.shallowDesc')); }
          else if (rv === 2) { lines.push(t('map.deep'), t('map.deepDesc')); }
        }
        const foodHere = data.food.find(f => f.x === gx && f.y === gy);
        if (foodHere) {
          const stageNames = ['Seed', 'Sprout', 'Adult', 'Decay'];
          const speciesNames = [t('map.speciesGrass'), t('map.speciesBush'), t('map.speciesTree')];
          const stageLabel = stageNames[(foodHere as any).stage] ?? '';
          const spLabel = speciesNames[(foodHere as any).species] ?? '';
          lines.push(
            `${spLabel || t('map.food')}${stageLabel ? ` [${stageLabel}]` : ''}`,
            `${t('map.energy')}: ${foodHere.energy.toFixed(1)} / ${foodHere.max_energy.toFixed(0)}`,
          );
        }
        if (showTerrain && data.heightMap?.length) {
          const h = data.heightMap[gy * gridW + gx]!;
          const s = data.slope?.[gy * gridW + gx] ?? 0;
          lines.push(`${t('map.height')}: ${h}`, `${t('map.slope')}: ${s.toFixed(1)}`);
        }
        const corpseHere = data.corpses.find(c => c.x === gx && c.y === gy);
        if (corpseHere) { lines.push(t('map.corpse'), `${t('map.energy')}: ${corpseHere.energy.toFixed(1)}`); }
        if (showTemp && data.temperatureGrid?.length) {
          lines.push(`${t('map.cellTemp')}: ${data.temperatureGrid[gy * gridW + gx]!.toFixed(1)}°C`);
        }
        if (showSurfaceWater && data.surfaceWater?.length) {
          const sw = data.surfaceWater[gy * gridW + gx] ?? 0;
          if (sw > 0.01) lines.push(`${t('map.surfaceWater')}: ${sw.toFixed(2)}`);
        }
        if (showGroundwater && data.groundwater?.length) {
          const gw = data.groundwater[gy * gridW + gx] ?? 0;
          lines.push(`${t('map.groundwater')}: ${(gw * 100).toFixed(0)}%`);
        }
        if (showPermeability && data.permeability?.length) {
          const p = data.permeability[gy * gridW + gx] ?? 1;
          lines.push(`${t('map.permeability')}: ${p.toFixed(2)}`);
        }
        if (showPressure && data.pressure?.length) {
          const p = data.pressure[gy * gridW + gx] ?? 1013;
          lines.push(`${t('map.pressure')}: ${p.toFixed(0)} hPa`);
        }
        if (showWind && data.windX?.length) {
          const wx = data.windX[gy * gridW + gx] ?? 0;
          const wy = data.windY[gy * gridW + gx] ?? 0;
          const wspd = Math.sqrt(wx * wx + wy * wy);
          lines.push(`${t('map.wind')}: ${wspd.toFixed(2)} (${wx.toFixed(1)}, ${wy.toFixed(1)})`);
        }
        if (showNutrient && data.nutrient?.length) {
          const nu = data.nutrient[gy * gridW + gx] ?? 0;
          if (nu > 0.01) lines.push(`${t('map.nutrient')}: ${nu.toFixed(1)}`);
        }
        if (showBiome && data.biome?.length) {
          const b = data.biome[gy * gridW + gx];
          if (b !== undefined) {
            const names = [t('map.biomeWater'), t('map.biomeRiverBank'), t('map.biomeDesert'), t('map.biomeGrassland'), t('map.biomeJungle'), t('map.biomeWetland'), t('map.biomeHighland'), t('map.biomeValley')];
            lines.push(`${t('map.biome')}: ${names[b] ?? '?'}`);
          }
        }
        setTooltip(lines.length > 0 ? { x: mx + 12, y: my - 10, text: lines } : null);
      }
    };

    const onMouseUp = () => { dragRef.current.dragging = false; };
    const onMouseLeave = () => { dragRef.current.dragging = false; setTooltip(null); };

    const onDblClick = (e: MouseEvent) => {
      const rect = canvas.getBoundingClientRect();
      const cam = camRef.current;
      const cellSize = cam.zoom * (rect.width / gridW);
      const gx = Math.floor((cam.x + (e.clientX - rect.left)) / cellSize);
      const gy = Math.floor((cam.y + (e.clientY - rect.top)) / cellSize);
      const data = drawDataRef.current;
      const clicked = data.agents.find(a => a.is_alive && a.x === gx && a.y === gy);
      setTrackedAgent(clicked ? (trackedAgent === clicked.id ? null : clicked.id) : null);
    };

    const onContextMenu = (e: MouseEvent) => {
      e.preventDefault();
      const rect = canvas.getBoundingClientRect();
      const cam = camRef.current;
      const cellSize = cam.zoom * (rect.width / gridW);
      const gx = Math.floor((cam.x + (e.clientX - rect.left)) / cellSize);
      const gy = Math.floor((cam.y + (e.clientY - rect.top)) / cellSize);
      const data = drawDataRef.current;
      const agent = data.agents.find(a => a.is_alive && a.x === gx && a.y === gy);
      if (agent) setTrackedAgent(agent.id);
    };

    canvas.addEventListener('wheel', onWheel, { passive: false });
    canvas.addEventListener('mousedown', onMouseDown);
    canvas.addEventListener('contextmenu', onContextMenu);
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
    canvas.addEventListener('mouseleave', onMouseLeave);
    canvas.addEventListener('dblclick', onDblClick);

    return () => {
      canvas.removeEventListener('wheel', onWheel);
      canvas.removeEventListener('mousedown', onMouseDown);
      canvas.removeEventListener('contextmenu', onContextMenu);
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
      canvas.removeEventListener('mouseleave', onMouseLeave);
      canvas.removeEventListener('dblclick', onDblClick);
    };
  }, [gridW, gridH, showTerrain, showTemp, showSurfaceWater, showGroundwater, showNutrient, showPermeability, showPressure, showWind, showBiome, t, trackedAgent, setTrackedAgent, drawDataRef]);

  return (
    <div className="w-full h-full relative">
      <canvas ref={canvasRef} className="w-full h-full block cursor-grab active:cursor-grabbing" />
      {trackedAgent !== null && (
        <button
          className="absolute top-2 right-2 text-[10px] px-2 py-0.5 rounded bg-zhi-panel border border-zhi-border text-zhi-muted hover:text-zhi-text z-10"
          onClick={() => setTrackedAgent(null)}
        >
          {t('map.untrack')}
        </button>
      )}
      {tooltip && (
        <div
          className="absolute pointer-events-none bg-zhi-panel/95 border border-zhi-border rounded px-2 py-1.5 text-[10px] text-zhi-text z-20 leading-relaxed"
          style={{ left: tooltip.x, top: tooltip.y }}
        >
          {tooltip.text.map((line, i) => <div key={i}>{line}</div>)}
        </div>
      )}
    </div>
  );
}, (prev, next) => {
  return prev.gridW === next.gridW
    && prev.gridH === next.gridH
    && prev.trackedAgent === next.trackedAgent
    && prev.showScent === next.showScent
    && prev.showFoodScent === next.showFoodScent
    && prev.showDirection === next.showDirection
    && prev.showVision === next.showVision
    && prev.showChemical === next.showChemical
    && prev.showTemp === next.showTemp
    && prev.showTerrain === next.showTerrain
    && prev.showFlow === next.showFlow
    && prev.showGroundwater === next.showGroundwater
    && prev.showSurfaceWater === next.showSurfaceWater
    && prev.showNutrient === next.showNutrient
    && prev.showPermeability === next.showPermeability
    && prev.showPressure === next.showPressure
    && prev.showBiome === next.showBiome
    && prev.showWind === next.showWind;
});
