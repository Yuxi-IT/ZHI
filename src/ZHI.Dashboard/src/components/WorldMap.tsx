import { useRef, useEffect, useState, memo } from 'react';
import type { AgentSnapshot, FoodTile, CorpseTile, WorldEvent } from '../types';
import { useT } from '../i18n/I18nContext';

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

  // Update draw function only when toggle/size settings change — does NOT restart rAF
  useEffect(() => {
    drawRef.current = () => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;

      const data = drawDataRef.current;
      const { agents, food, corpses, river, scent, foodScent, chemicalField,
        temperatureGrid, heightMap, slope, riverFlow,
        surfaceWater, groundwater, nutrient, permeability, pressure, windX, windY, events, timeOfDay } = data;
      const tracked = trackedProp !== undefined ? trackedProp : data.trackedAgent;

      // Process new events → floating texts (use monotonic _eid to survive clears/truncation)
      if (events && events.length > 0) {
        const newEvents: typeof events = [];
        for (let i = events.length - 1; i >= 0; i--) {
          const eid = (events[i]! as any)._eid as number | undefined;
          if (eid === undefined || eid <= lastEidRef.current) break;
          newEvents.unshift(events[i]!);
        }
        if (newEvents.length > 0) {
          lastEidRef.current = (events[events.length - 1] as any)._eid ?? lastEidRef.current;
          const now = performance.now();
          for (const ev of newEvents) {
            const agent = agents.find(a => a.id === ev.agent_id);

            if (ev.type === 'attack' && ev.target_id !== undefined) {
              const target = agents.find(a => a.id === ev.target_id);
              const tx = target?.x ?? agent?.x ?? 0;
              const ty = target?.y ?? agent?.y ?? 0;
              floatingTextsRef.current.push({
                id: floatingIdRef.current++,
                x: tx, y: ty,
                text: `-${ev.value.toFixed(0)}`,
                color: '#ef4444', startTime: now,
              });
              continue;
            }

            if (!agent) continue;

            let text = '', color = '';
            if (ev.type === 'eat') { text = `+${ev.value.toFixed(0)}`; color = '#22c55e'; }
            else if (ev.type === 'death') { text = 'DEAD'; color = '#94a3b8'; }
            else if (ev.type === 'respawn') { text = 'RESPAWN'; color = '#a78bfa'; }
            else { continue; }

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

      if (tracked !== null) {
        const agent = agents.find(a => a.id === tracked && a.is_alive);
        if (agent) {
          cam.x = agent.x * cellSize + cellSize / 2 - w / 2;
          cam.y = agent.y * cellSize + cellSize / 2 - h / 2;
        }
      }

      const brightness = (Math.cos((timeOfDay - 14) * Math.PI / 12) + 1) / 2;
      const bgBase = Math.round(5 + brightness * 25);
      ctx.fillStyle = `rgb(${bgBase},${bgBase},${bgBase})`;
      ctx.fillRect(0, 0, w, h);

      ctx.save();
      ctx.translate(-cam.x, -cam.y);

      const totalW = gridW * cellSize, totalH = gridH * cellSize;
      const gridBg = Math.round(8 + brightness * 20);
      ctx.fillStyle = `rgb(${gridBg},${gridBg},${gridBg})`;
      ctx.fillRect(0, 0, totalW, totalH);

      if (cellSize > 8) {
        const lineBase = Math.round(15 + brightness * 30);
        ctx.strokeStyle = `rgb(${lineBase},${lineBase},${lineBase})`;
        ctx.lineWidth = 0.5;
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let i = sc; i <= ec; i++) { ctx.beginPath(); ctx.moveTo(i * cellSize, sr * cellSize); ctx.lineTo(i * cellSize, er * cellSize); ctx.stroke(); }
        for (let j = sr; j <= er; j++) { ctx.beginPath(); ctx.moveTo(sc * cellSize, j * cellSize); ctx.lineTo(ec * cellSize, j * cellSize); ctx.stroke(); }
      }

      // Water
      if (river.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = river[gy * gridW + gx];
            if (val === 0) continue;
            ctx.fillStyle = val === 2 ? 'rgba(30, 64, 175, 0.6)' : 'rgba(59, 130, 246, 0.35)';
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Height map
      if (showTerrain && heightMap && heightMap.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const h = heightMap[gy * gridW + gx]!;
            const v = h / 255;
            ctx.fillStyle = `rgba(${Math.round(v * 255)},${Math.round(v * 255)},${Math.round(v * 255)},0.15)`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // River flow
      if (showFlow && riverFlow && riverFlow.length > 0 && cellSize > 8) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        const dirs = [0, -Math.PI / 2, -Math.PI / 4, 0, Math.PI / 4, Math.PI / 2, 3 * Math.PI / 4, Math.PI, -3 * Math.PI / 4];
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const flow = riverFlow[gy * gridW + gx] ?? 0;
            if (flow <= 0 || flow > 8) continue;
            const cx = gx * cellSize + cellSize / 2, cy = gy * cellSize + cellSize / 2;
            const ang = dirs[flow]!, al = cellSize * 0.3;
            ctx.fillStyle = 'rgba(255, 255, 255, 0.35)';
            ctx.beginPath();
            ctx.moveTo(cx + Math.cos(ang) * al, cy + Math.sin(ang) * al);
            ctx.lineTo(cx + Math.cos(ang + 2.5) * al * 0.5, cy + Math.sin(ang + 2.5) * al * 0.5);
            ctx.lineTo(cx + Math.cos(ang - 2.5) * al * 0.5, cy + Math.sin(ang - 2.5) * al * 0.5);
            ctx.closePath(); ctx.fill();
          }
        }
      }

      // Food scent
      if (showFoodScent && foodScent && foodScent.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = foodScent[gy * gridW + gx]!;
            if (val <= 0.01) continue;
            ctx.fillStyle = `rgba(34, 197, 94, ${Math.min(val / 10, 0.5)})`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Agent scent
      if (showScent && scent.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = scent[gy * gridW + gx]!;
            if (val <= 0.01) continue;
            ctx.fillStyle = `rgba(168, 85, 247, ${Math.min(val / 10, 0.6)})`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Signal field
      if (showChemical && chemicalField && chemicalField.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = chemicalField[gy * gridW + gx]!;
            if (val <= 0.01) continue;
            ctx.fillStyle = `rgba(250, 204, 21, ${Math.min(val, 0.5)})`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Temperature
      if (showTemp && temperatureGrid && temperatureGrid.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const tp = temperatureGrid[gy * gridW + gx]!;
            let r: number, g: number, b: number;
            if (tp < 5) { r = 59; g = 130; b = 246; }
            else if (tp < 15) { const s = (tp - 5) / 10; r = Math.round(59 + s * 89); g = Math.round(130 + s * 33); b = Math.round(246 + s * -16); }
            else if (tp < 25) { const s = (tp - 15) / 10; r = Math.round(148 + s * -74); g = Math.round(163 + s * 59); b = Math.round(230 + s * -8); }
            else if (tp < 35) { const s = (tp - 25) / 10; r = Math.round(74 + s * 165); g = Math.round(222 + s * -154); b = Math.round(222 + s * -154); }
            else { r = 239; g = 68; b = 68; }
            ctx.fillStyle = `rgba(${r},${g},${b},0.25)`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Surface water
      if (showSurfaceWater && surfaceWater && surfaceWater.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = surfaceWater[gy * gridW + gx]!;
            if (val <= 0.01) continue;
            ctx.fillStyle = `rgba(59, 130, 246, ${Math.min(val / 3, 0.6)})`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Groundwater
      if (showGroundwater && groundwater && groundwater.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = groundwater[gy * gridW + gx]!;
            if (val <= 0.01) continue;
            ctx.fillStyle = `rgba(30, 64, 175, ${Math.min(val, 0.5)})`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Nutrient
      if (showNutrient && nutrient && nutrient.length > 0) {
        const maxNutrient = 10;
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = nutrient[gy * gridW + gx]!;
            if (val <= 0.01) continue;
            const t = Math.min(val / maxNutrient, 1);
            const r = Math.round(34 + t * 146);
            const g = Math.round(139 + t * 50);
            const b = Math.round(34 - t * 10);
            ctx.fillStyle = `rgba(${r},${g},${b},0.35)`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Permeability
      if (showPermeability && permeability && permeability.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const val = permeability[gy * gridW + gx]!;
            const t = Math.max(0, Math.min(1, (val - 0.2) / 1.8));
            ctx.fillStyle = `rgba(${Math.round(75 * (1 - t) + 34 * t)},${Math.round(85 * (1 - t) + 197 * t)},${Math.round(160 * (1 - t) + 94 * t)},0.25)`;
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Pressure
      if (showPressure && pressure && pressure.length > 0) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        for (let gx = sc; gx < ec; gx++) {
          for (let gy = sr; gy < er; gy++) {
            const p = pressure[gy * gridW + gx]!;
            const t = (p - 1000) / 30; // normalize around 1000-1030 hPa
            const ct = Math.max(-1, Math.min(1, t));
            if (ct < 0) {
              // Low pressure: warm colors
              ctx.fillStyle = `rgba(${Math.round(239 + ct * 50)},${Math.round(68 - ct * 30)},${Math.round(68 - ct * 30)},0.2)`;
            } else {
              // High pressure: cool colors
              ctx.fillStyle = `rgba(${Math.round(59 - ct * 30)},${Math.round(130 - ct * 50)},${Math.round(246 - ct * 50)},0.2)`;
            }
            ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
          }
        }
      }

      // Wind arrows
      if (showWind && windX && windY && windX.length > 0 && cellSize > 6) {
        const sc = Math.max(0, Math.floor(cam.x / cellSize));
        const ec = Math.min(gridW, Math.ceil((cam.x + w) / cellSize));
        const sr = Math.max(0, Math.floor(cam.y / cellSize));
        const er = Math.min(gridH, Math.ceil((cam.y + h) / cellSize));
        const arrowStep = Math.max(1, Math.floor(8 / cam.zoom));
        for (let gx = sc; gx < ec; gx += arrowStep) {
          for (let gy = sr; gy < er; gy += arrowStep) {
            const wx = windX[gy * gridW + gx]!;
            const wy = windY[gy * gridW + gx]!;
            const mag = Math.sqrt(wx * wx + wy * wy);
            if (mag < 0.05) continue;
            const cx = gx * cellSize + cellSize / 2;
            const cy = gy * cellSize + cellSize / 2;
            const len = Math.min(mag * cellSize * 3, cellSize * 1.5);
            const nx = wx / mag, ny = wy / mag;
            const ex = cx + nx * len, ey = cy + ny * len;
            ctx.strokeStyle = `rgba(255, 255, 255, ${Math.min(0.6, mag * 2)})`;
            ctx.lineWidth = Math.max(0.5, mag * 1.5);
            ctx.beginPath();
            ctx.moveTo(cx, cy);
            ctx.lineTo(ex, ey);
            ctx.stroke();
            // Arrowhead
            if (len > 3) {
              const ah = len * 0.3;
              ctx.beginPath();
              ctx.moveTo(ex, ey);
              ctx.lineTo(ex - nx * ah + ny * ah * 0.5, ey - ny * ah - nx * ah * 0.5);
              ctx.lineTo(ex - nx * ah - ny * ah * 0.5, ey - ny * ah + nx * ah * 0.5);
              ctx.closePath();
              ctx.fill();
            }
          }
        }
      }

      // Corpses
      const corpseSz = Math.max(cellSize * 0.6, 2);
      for (const c of corpses) {
        const alpha = Math.max(0.2, Math.min(1, c.energy / 20));
        ctx.fillStyle = `rgba(148, 163, 184, ${alpha})`;
        const cx = c.x * cellSize + cellSize / 2, cy = c.y * cellSize + cellSize / 2;
        ctx.beginPath();
        ctx.moveTo(cx, cy - corpseSz / 2); ctx.lineTo(cx + corpseSz / 2, cy);
        ctx.lineTo(cx, cy + corpseSz / 2); ctx.lineTo(cx - corpseSz / 2, cy);
        ctx.closePath(); ctx.fill();
      }

      // Plants (formerly Food)
      const foodSz = Math.max(cellSize * 0.7, 2);
      for (const f of food) {
        const energyRatio = f.max_energy > 0 ? f.energy / f.max_energy : 1;
        const alpha = Math.max(0.2, energyRatio);
        ctx.fillStyle = `rgba(34, 197, 94, ${alpha})`;
        ctx.fillRect(f.x * cellSize + (cellSize - foodSz) / 2, f.y * cellSize + (cellSize - foodSz) / 2, foodSz, foodSz);
      }

      // Vision
      if (showVision) {
        const baseMask = [
          [1,1,1,1,1,1,1],
          [0,1,1,1,1,1,0],
          [0,0,1,1,1,0,0],
          [0,0,0,1,0,0,0],
          [0,0,0,0,0,0,0],
          [0,0,0,0,0,0,0],
          [0,0,0,0,0,0,0],
        ];
        for (const agent of agents) {
          if (!agent.is_alive) continue;
          const agentHeight = heightMap ? (heightMap[agent.y * gridW + agent.x] ?? 128) / 255 : 0.5;
          const R = 3, D = 7, fd = agent.facing_direction;
          for (let dy = -R; dy <= R; dy++) {
            for (let dx = -R; dx <= R; dx++) {
              let rdx: number, rdy: number;
              if (fd === 0) { rdx = dx; rdy = dy; }
              else if (fd === 1) { rdx = -dx; rdy = -dy; }
              else if (fd === 2) { rdx = -dy; rdy = dx; }
              else { rdx = dy; rdy = -dx; }
              const mc = rdx + R, mr = rdy + R;
              if (mc < 0 || mc >= D || mr < 0 || mr >= D) continue;
              if (!baseMask[mr]![mc]) {
                const rowDepth = mr / (D - 1);
                const visReq = rowDepth * 1.5;
                if (agentHeight < visReq - 0.1) continue;
              }
              const gx = agent.x + dx, gy = agent.y + dy;
              if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) continue;
              const dist = Math.abs(dx) + Math.abs(dy);
              ctx.fillStyle = `rgba(255, 255, 255, ${dist === 0 ? 0.08 : dist <= 2 ? 0.04 : 0.02})`;
              ctx.fillRect(gx * cellSize, gy * cellSize, cellSize, cellSize);
            }
          }
        }
      }

      // Agents
      for (const agent of agents) {
        if (!agent.is_alive) continue;
        const cx = agent.x * cellSize + cellSize / 2;
        const cy = agent.y * cellSize + cellSize / 2;
        const r = Math.max(cellSize * 0.4, 3);
        const hp = Math.max(0, Math.min(1, agent.energy / 100));
        ctx.fillStyle = `hsl(${hp * 120}, 70%, 50%)`;

        if (agent.stress > 0.5) { ctx.shadowColor = 'rgba(239, 68, 68, 0.6)'; ctx.shadowBlur = agent.stress * 6; }
        ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2); ctx.fill();
        ctx.shadowColor = 'transparent'; ctx.shadowBlur = 0;

        if (agent.is_stationary) {
          ctx.strokeStyle = 'rgba(147, 197, 253, 0.6)'; ctx.lineWidth = 1.5;
          ctx.beginPath(); ctx.arc(cx, cy, r + 2, 0, Math.PI * 2); ctx.stroke();
        }

        if (showDirection && cellSize > 6) {
          const dirs = [-Math.PI / 2, Math.PI / 2, Math.PI, 0];
          const angle = dirs[agent.facing_direction] ?? Math.PI / 2;
          const ad = r + 2, al = Math.max(cellSize * 0.2, 2);
          const ax = cx + Math.cos(angle) * ad, ay = cy + Math.sin(angle) * ad;
          ctx.fillStyle = 'rgba(255, 255, 255, 0.6)';
          ctx.beginPath();
          ctx.moveTo(ax + Math.cos(angle) * al, ay + Math.sin(angle) * al);
          ctx.lineTo(ax + Math.cos(angle + 2.4) * al * 0.6, ay + Math.sin(angle + 2.4) * al * 0.6);
          ctx.lineTo(ax + Math.cos(angle - 2.4) * al * 0.6, ay + Math.sin(angle - 2.4) * al * 0.6);
          ctx.closePath(); ctx.fill();
        }

        if (agent.is_stationary && cellSize > 10) {
          ctx.fillStyle = 'rgba(147, 197, 253, 0.8)';
          ctx.font = `${Math.max(8, cellSize * 0.25)}px monospace`;
          ctx.textAlign = 'center'; ctx.textBaseline = 'bottom';
          ctx.fillText(t('map.zzz'), cx, cy - r - 3);
        }

        if (agent.id === tracked) {
          ctx.strokeStyle = '#facc15'; ctx.lineWidth = 2;
          ctx.beginPath(); ctx.arc(cx, cy, r + 3, 0, Math.PI * 2); ctx.stroke();
        }

        if (cellSize > 12) {
          ctx.fillStyle = '#fff';
          ctx.font = `${Math.max(9, cellSize * 0.3)}px monospace`;
          ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
          ctx.fillText(String(agent.id), cx, cy);
        }
      }

      // Floating text
      const now = performance.now();
      const FADE_MS = 1200;
      floatingTextsRef.current = floatingTextsRef.current.filter(ft => now - ft.startTime < FADE_MS);
      for (const ft of floatingTextsRef.current) {
        const progress = (now - ft.startTime) / FADE_MS;
        const alpha = 1 - progress;
        const fy = ft.y * cellSize - progress * cellSize * 1.5;
        ctx.globalAlpha = alpha;
        ctx.fillStyle = ft.color;
        ctx.font = `bold ${Math.max(10, cellSize * 0.5)}px monospace`;
        ctx.textAlign = 'center'; ctx.textBaseline = 'bottom';
        ctx.fillText(ft.text, ft.x * cellSize + cellSize / 2, fy);
      }
      ctx.globalAlpha = 1;

      ctx.restore();

      // HUD
      ctx.fillStyle = 'rgba(255,255,255,0.4)';
      ctx.font = '10px monospace'; ctx.textAlign = 'left'; ctx.textBaseline = 'top';
      const zoomPct = Math.round(cam.zoom * 100);
      const alive = agents.filter(a => a.is_alive).length;
      const hud = tracked !== null
        ? t('map.hudTracking', { zoom: zoomPct, id: tracked, alive, total: agents.length })
        : t('map.hudNormal', { zoom: zoomPct, alive, total: agents.length });
      ctx.fillText(hud, 8, 8);
    };
  }, [showScent, showFoodScent, showDirection, showVision, showChemical, showTemp, showTerrain, showFlow, showGroundwater, showSurfaceWater, showNutrient, showPermeability, showPressure, showWind, gridW, gridH, t, trackedProp, drawDataRef]);

  // Stable rAF loop — starts once, never restarts
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

  // Mouse event handlers — all read from drawDataRef.current for data lookups
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
        // Inline tooltip computation from drawDataRef
        const { agents, food, corpses, river, heightMap, slope, temperatureGrid, surfaceWater, groundwater, nutrient, permeability, pressure, windX, windY } = drawDataRef.current;
        const cam = camRef.current;
        const cellSize = cam.zoom * (rect.width / gridW);
        const gx = Math.floor((cam.x + mx) / cellSize);
        const gy = Math.floor((cam.y + my) / cellSize);
        if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) { setTooltip(null); return; }

        const lines: string[] = [];
        const agent = agents.find(a => a.is_alive && a.x === gx && a.y === gy);
        if (agent) {
          lines.push(
            t('map.tooltipAgent', { id: agent.id }) + (agent.respawn_count > 0 ? ` ${t('map.tooltipGen', { gen: agent.respawn_count })}` : ''),
            `${t('map.tooltipEnergy')}: ${agent.energy.toFixed(1)}  ${t('agents.stress')}: ${agent.stress.toFixed(2)}`,
            `${t('agents.water')}: ${agent.water.toFixed(1)}  ${t('map.tooltipBTemp')}: ${agent.body_temperature.toFixed(1)}°C  ${t('agents.age')}: ${agent.tick_count}`,
            `${t('agents.action')}: ${agent.is_eating ? '\u{1F356} ' : ''}${agent.last_action || t('agents.none')}`,
            `${t('map.tooltipEats')}: ${agent.eat_count}  ${t('map.tooltipAttacks')}: ${agent.attack_count}  ${t('map.tooltipEmits')}: ${agent.emit_count}`,
          );
        }
        if (river.length > 0) {
          const rv = river[gy * gridW + gx];
          if (rv === 1) { lines.push(t('map.shallow'), t('map.shallowDesc')); }
          else if (rv === 2) { lines.push(t('map.deep'), t('map.deepDesc')); }
        }
        const foodHere = food.find(f => f.x === gx && f.y === gy);
        if (foodHere) {
          lines.push(
            t('map.food'),
            `${t('map.energy')}: ${foodHere.energy.toFixed(1)} / ${foodHere.max_energy.toFixed(0)}`,
          );
        }
        if (showTerrain && heightMap && heightMap.length > 0) {
          const h = heightMap[gy * gridW + gx]!;
          const s = slope?.[gy * gridW + gx] ?? 0;
          lines.push(`${t('map.height')}: ${h}`, `${t('map.slope')}: ${s.toFixed(1)}`);
        }
        const corpseHere = corpses.find(c => c.x === gx && c.y === gy);
        if (corpseHere) { lines.push(t('map.corpse'), `${t('map.energy')}: ${corpseHere.energy.toFixed(1)}`); }
        if (showTemp && temperatureGrid && temperatureGrid.length > 0) {
          lines.push(`${t('map.cellTemp')}: ${temperatureGrid[gy * gridW + gx]!.toFixed(1)}°C`);
        }
        if (showSurfaceWater && surfaceWater && surfaceWater.length > 0) {
          const sw = surfaceWater[gy * gridW + gx] ?? 0;
          if (sw > 0.01) lines.push(`${t('map.surfaceWater')}: ${sw.toFixed(2)}`);
        }
        if (showGroundwater && groundwater && groundwater.length > 0) {
          const gw = groundwater[gy * gridW + gx] ?? 0;
          lines.push(`${t('map.groundwater')}: ${(gw * 100).toFixed(0)}%`);
        }
        if (showPermeability && permeability && permeability.length > 0) {
          const p = permeability[gy * gridW + gx] ?? 1;
          lines.push(`${t('map.permeability')}: ${p.toFixed(2)}`);
        }
        if (showPressure && pressure && pressure.length > 0) {
          const p = pressure[gy * gridW + gx] ?? 1013;
          lines.push(`${t('map.pressure')}: ${p.toFixed(0)} hPa`);
        }
        if (showWind && windX && windY && windX.length > 0) {
          const wx = windX[gy * gridW + gx] ?? 0;
          const wy = windY[gy * gridW + gx] ?? 0;
          const wspd = Math.sqrt(wx * wx + wy * wy);
          lines.push(`${t('map.wind')}: ${wspd.toFixed(2)} (${wx.toFixed(1)}, ${wy.toFixed(1)})`);
        }
        if (showNutrient && nutrient && nutrient.length > 0) {
          const nu = nutrient[gy * gridW + gx] ?? 0;
          if (nu > 0.01) lines.push(`${t('map.nutrient')}: ${nu.toFixed(1)}`);
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
  }, [gridW, gridH, showTerrain, showTemp, showSurfaceWater, showGroundwater, showNutrient, showPermeability, showPressure, showWind, t, trackedAgent, setTrackedAgent, drawDataRef]);

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
    && prev.showWind === next.showWind;
});
