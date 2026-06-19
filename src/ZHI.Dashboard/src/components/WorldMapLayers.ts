// Pure rendering functions for each layer of the 2D WorldMap canvas.
// Separated from WorldMap.tsx to keep the component clean.

const BIOME_COLORS: [number, number, number][] = [
  [30, 64, 175],   // 0 Water
  [34, 197, 94],   // 1 RiverBank
  [245, 158, 11],  // 2 Desert
  [132, 204, 22],  // 3 Grassland
  [22, 101, 52],   // 4 Jungle
  [14, 165, 233],  // 5 Wetland
  [148, 163, 184], // 6 Highland
  [251, 191, 36],  // 7 Valley
];

const PLANT_STAGE_COLORS: Record<number, [number, number, number][]> = {
  0: [[139, 90, 43],  [120, 80, 40],  [100, 70, 35]],
  1: [[144, 238, 144],[100, 200, 80], [80, 160, 60]],
  2: [[34, 197, 94],  [20, 150, 60], [15, 120, 45]],
  3: [[128, 128, 128],[110, 100, 90],[90, 80, 70]],
};

interface LayerCtx {
  ctx: CanvasRenderingContext2D;
  w: number; h: number;
  camX: number; camY: number;
  cellSize: number;
  brightness: number;
}

function visibleRect(l: LayerCtx, gridW: number, gridH: number) {
  return {
    sc: Math.max(0, Math.floor(l.camX / l.cellSize)),
    ec: Math.min(gridW, Math.ceil((l.camX + l.w) / l.cellSize)),
    sr: Math.max(0, Math.floor(l.camY / l.cellSize)),
    er: Math.min(gridH, Math.ceil((l.camY + l.h) / l.cellSize)),
  };
}

// ---- Background ----

export function drawBackground(l: LayerCtx) {
  const bg = Math.round(5 + l.brightness * 25);
  l.ctx.fillStyle = `rgb(${bg},${bg},${bg})`;
  l.ctx.fillRect(0, 0, l.w, l.h);
}

export function drawGridBg(l: LayerCtx, gridW: number, gridH: number) {
  const bg = Math.round(8 + l.brightness * 20);
  l.ctx.fillStyle = `rgb(${bg},${bg},${bg})`;
  l.ctx.fillRect(0, 0, gridW * l.cellSize, gridH * l.cellSize);
}

export function drawGridLines(l: LayerCtx, gridW: number, gridH: number) {
  if (l.cellSize <= 8) return;
  const { sc, ec, sr, er } = visibleRect(l, gridW, gridH);
  const line = Math.round(15 + l.brightness * 30);
  l.ctx.strokeStyle = `rgb(${line},${line},${line})`;
  l.ctx.lineWidth = 0.5;
  for (let i = sc; i <= ec; i++) { l.ctx.beginPath(); l.ctx.moveTo(i * l.cellSize, sr * l.cellSize); l.ctx.lineTo(i * l.cellSize, er * l.cellSize); l.ctx.stroke(); }
  for (let j = sr; j <= er; j++) { l.ctx.beginPath(); l.ctx.moveTo(sc * l.cellSize, j * l.cellSize); l.ctx.lineTo(ec * l.cellSize, j * l.cellSize); l.ctx.stroke(); }
}

// ---- Grid overlays ----

export function drawRiver(l: LayerCtx, river: number[], gridW: number, gridH: number) {
  const { sc, ec, sr, er } = visibleRect(l, gridW, gridH);
  for (let gx = sc; gx < ec; gx++)
    for (let gy = sr; gy < er; gy++) {
      const v = river[gy * gridW + gx];
      if (v === 0) continue;
      l.ctx.fillStyle = v === 2 ? 'rgba(30, 64, 175, 0.6)' : 'rgba(59, 130, 246, 0.35)';
      l.ctx.fillRect(gx * l.cellSize, gy * l.cellSize, l.cellSize, l.cellSize);
    }
}

export function drawHeightMap(l: LayerCtx, heightMap: number[], gridW: number, gridH: number) {
  const { sc, ec, sr, er } = visibleRect(l, gridW, gridH);
  for (let gx = sc; gx < ec; gx++)
    for (let gy = sr; gy < er; gy++) {
      const h = heightMap[gy * gridW + gx]!;
      const v = h / 255;
      l.ctx.fillStyle = `rgba(${Math.round(v * 255)},${Math.round(v * 255)},${Math.round(v * 255)},0.15)`;
      l.ctx.fillRect(gx * l.cellSize, gy * l.cellSize, l.cellSize, l.cellSize);
    }
}

export function drawRiverFlow(l: LayerCtx, riverFlow: number[], gridW: number, gridH: number) {
  if (l.cellSize <= 8) return;
  const { sc, ec, sr, er } = visibleRect(l, gridW, gridH);
  const dirs = [0, -Math.PI / 2, -Math.PI / 4, 0, Math.PI / 4, Math.PI / 2, 3 * Math.PI / 4, Math.PI, -3 * Math.PI / 4];
  for (let gx = sc; gx < ec; gx++)
    for (let gy = sr; gy < er; gy++) {
      const flow = riverFlow[gy * gridW + gx] ?? 0;
      if (flow <= 0 || flow > 8) continue;
      const cx = gx * l.cellSize + l.cellSize / 2, cy = gy * l.cellSize + l.cellSize / 2;
      const ang = dirs[flow]!, al = l.cellSize * 0.3;
      l.ctx.fillStyle = 'rgba(255, 255, 255, 0.35)';
      l.ctx.beginPath();
      l.ctx.moveTo(cx + Math.cos(ang) * al, cy + Math.sin(ang) * al);
      l.ctx.lineTo(cx + Math.cos(ang + 2.5) * al * 0.5, cy + Math.sin(ang + 2.5) * al * 0.5);
      l.ctx.lineTo(cx + Math.cos(ang - 2.5) * al * 0.5, cy + Math.sin(ang - 2.5) * al * 0.5);
      l.ctx.closePath(); l.ctx.fill();
    }
}

function drawScalarOverlay(l: LayerCtx, data: number[], gridW: number, gridH: number,
  colorFn: (v: number) => string, skipPred: (v: number) => boolean = v => v <= 0.01) {
  const { sc, ec, sr, er } = visibleRect(l, gridW, gridH);
  for (let gx = sc; gx < ec; gx++)
    for (let gy = sr; gy < er; gy++) {
      const v = data[gy * gridW + gx]!;
      if (skipPred(v)) continue;
      l.ctx.fillStyle = colorFn(v);
      l.ctx.fillRect(gx * l.cellSize, gy * l.cellSize, l.cellSize, l.cellSize);
    }
}

export function drawFoodScent(l: LayerCtx, foodScent: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, foodScent, gridW, gridH, v => `rgba(34, 197, 94, ${Math.min(v / 10, 0.5)})`);
}

export function drawAgentScent(l: LayerCtx, scent: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, scent, gridW, gridH, v => `rgba(168, 85, 247, ${Math.min(v / 10, 0.6)})`);
}

export function drawChemical(l: LayerCtx, chemicalField: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, chemicalField, gridW, gridH, v => `rgba(250, 204, 21, ${Math.min(v, 0.5)})`);
}

export function drawTemperature(l: LayerCtx, temperatureGrid: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, temperatureGrid, gridW, gridH, tp => {
    let r: number, g: number, b: number;
    if (tp < 5) { r = 59; g = 130; b = 246; }
    else if (tp < 15) { const s = (tp - 5) / 10; r = Math.round(59 + s * 89); g = Math.round(130 + s * 33); b = Math.round(246 + s * -16); }
    else if (tp < 25) { const s = (tp - 15) / 10; r = Math.round(148 + s * -74); g = Math.round(163 + s * 59); b = Math.round(230 + s * -8); }
    else if (tp < 35) { const s = (tp - 25) / 10; r = Math.round(74 + s * 165); g = Math.round(222 + s * -154); b = Math.round(222 + s * -154); }
    else { r = 239; g = 68; b = 68; }
    return `rgba(${r},${g},${b},0.25)`;
  }, v => v < -99);
}

export function drawSurfaceWater(l: LayerCtx, surfaceWater: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, surfaceWater, gridW, gridH, v => `rgba(59, 130, 246, ${Math.min(v / 3, 0.6)})`);
}

export function drawGroundwater(l: LayerCtx, groundwater: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, groundwater, gridW, gridH, v => `rgba(30, 64, 175, ${Math.min(v, 0.5)})`);
}

export function drawNutrient(l: LayerCtx, nutrient: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, nutrient, gridW, gridH, v => {
    const t = Math.min(v / 10, 1);
    return `rgba(${Math.round(34 + t * 146)},${Math.round(139 + t * 50)},${Math.round(34 - t * 10)},0.35)`;
  });
}

export function drawPermeability(l: LayerCtx, permeability: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, permeability, gridW, gridH, v => {
    const t = Math.max(0, Math.min(1, (v - 0.2) / 1.8));
    return `rgba(${Math.round(75 * (1 - t) + 34 * t)},${Math.round(85 * (1 - t) + 197 * t)},${Math.round(160 * (1 - t) + 94 * t)},0.25)`;
  }, v => v <= 0);
}

export function drawPressure(l: LayerCtx, pressure: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, pressure, gridW, gridH, p => {
    const t = (p - 1000) / 30;
    const ct = Math.max(-1, Math.min(1, t));
    if (ct < 0) return `rgba(${Math.round(239 + ct * 50)},${Math.round(68 - ct * 30)},${Math.round(68 - ct * 30)},0.2)`;
    return `rgba(${Math.round(59 - ct * 30)},${Math.round(130 - ct * 50)},${Math.round(246 - ct * 50)},0.2)`;
  }, v => v <= 0);
}

export function drawWind(l: LayerCtx, windX: number[], windY: number[], gridW: number, gridH: number) {
  if (l.cellSize <= 6) return;
  const { sc, ec, sr, er } = visibleRect(l, gridW, gridH);
  const zoom = l.cellSize * gridW / l.w;
  const step = Math.max(1, Math.floor(8 / zoom));
  for (let gx = sc; gx < ec; gx += step)
    for (let gy = sr; gy < er; gy += step) {
      const wx = windX[gy * gridW + gx]!;
      const wy = windY[gy * gridW + gx]!;
      const mag = Math.sqrt(wx * wx + wy * wy);
      if (mag < 0.05) continue;
      const cx = gx * l.cellSize + l.cellSize / 2, cy = gy * l.cellSize + l.cellSize / 2;
      const len = Math.min(mag * l.cellSize * 3, l.cellSize * 1.5);
      const nx = wx / mag, ny = wy / mag;
      const ex = cx + nx * len, ey = cy + ny * len;
      l.ctx.strokeStyle = `rgba(255, 255, 255, ${Math.min(0.6, mag * 2)})`;
      l.ctx.lineWidth = Math.max(0.5, mag * 1.5);
      l.ctx.beginPath(); l.ctx.moveTo(cx, cy); l.ctx.lineTo(ex, ey); l.ctx.stroke();
      if (len > 3) {
        const ah = len * 0.3;
        l.ctx.beginPath();
        l.ctx.moveTo(ex, ey);
        l.ctx.lineTo(ex - nx * ah + ny * ah * 0.5, ey - ny * ah - nx * ah * 0.5);
        l.ctx.lineTo(ex - nx * ah - ny * ah * 0.5, ey - ny * ah + nx * ah * 0.5);
        l.ctx.closePath(); l.ctx.fill();
      }
    }
}

export function drawBiome(l: LayerCtx, biome: number[], gridW: number, gridH: number) {
  drawScalarOverlay(l, biome, gridW, gridH, b => {
    const [r, g, bl] = BIOME_COLORS[b] ?? [128, 128, 128];
    return `rgba(${r},${g},${bl},0.15)`;
  }, v => v < 0);
}

// ---- Entities ----

export function drawCorpses(l: LayerCtx, corpses: { x: number; y: number; energy: number }[]) {
  const sz = Math.max(l.cellSize * 0.6, 2);
  for (const c of corpses) {
    const alpha = Math.max(0.2, Math.min(1, c.energy / 20));
    l.ctx.fillStyle = `rgba(148, 163, 184, ${alpha})`;
    const cx = c.x * l.cellSize + l.cellSize / 2, cy = c.y * l.cellSize + l.cellSize / 2;
    l.ctx.beginPath();
    l.ctx.moveTo(cx, cy - sz / 2); l.ctx.lineTo(cx + sz / 2, cy);
    l.ctx.lineTo(cx, cy + sz / 2); l.ctx.lineTo(cx - sz / 2, cy);
    l.ctx.closePath(); l.ctx.fill();
  }
}

export function drawPlants(l: LayerCtx, food: any[]) {
  for (const f of food) {
    const stage: number = (f as any).stage ?? 2;
    const sp: number = (f as any).species ?? 0;
    const colors = PLANT_STAGE_COLORS[stage] ?? PLANT_STAGE_COLORS[2]!;
    const [cr, cg, cb] = colors[Math.min(sp, 2)]!;
    const energyRatio = f.max_energy > 0 ? f.energy / f.max_energy : 1;
    let sz = Math.max(l.cellSize * 0.7, 2);
    if (stage === 0) sz = Math.max(l.cellSize * 0.35, 1.5);
    else if (stage === 1) sz = Math.max(l.cellSize * 0.55, 1.8);
    else if (stage === 3) sz = Math.max(l.cellSize * 0.8, 2.2);
    const alpha = stage === 0 ? 0.4 : stage === 3 ? Math.max(0.15, energyRatio * 0.5) : Math.max(0.25, energyRatio);
    l.ctx.fillStyle = `rgba(${cr}, ${cg}, ${cb}, ${alpha})`;
    l.ctx.fillRect(f.x * l.cellSize + (l.cellSize - sz) / 2, f.y * l.cellSize + (l.cellSize - sz) / 2, sz, sz);
  }
}

export function drawVision(l: LayerCtx, agents: any[], heightMap: number[] | undefined, gridW: number, gridH: number) {
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
    for (let dy = -R; dy <= R; dy++)
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
          if (agentHeight < rowDepth * 1.5 - 0.1) continue;
        }
        const gx = agent.x + dx, gy = agent.y + dy;
        if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) continue;
        const dist = Math.abs(dx) + Math.abs(dy);
        l.ctx.fillStyle = `rgba(255, 255, 255, ${dist === 0 ? 0.08 : dist <= 2 ? 0.04 : 0.02})`;
        l.ctx.fillRect(gx * l.cellSize, gy * l.cellSize, l.cellSize, l.cellSize);
      }
  }
}

export function drawAgents(l: LayerCtx, agents: any[], tracked: number | null,
  showDirection: boolean, zzzLabel: string, cellSize: number) {
  for (const agent of agents) {
    if (!agent.is_alive) continue;
    const cx = agent.x * cellSize + cellSize / 2;
    const cy = agent.y * cellSize + cellSize / 2;
    const r = Math.max(cellSize * 0.4, 3);
    const hp = Math.max(0, Math.min(1, agent.energy / 100));
    l.ctx.fillStyle = `hsl(${hp * 120}, 70%, 50%)`;

    if (agent.stress > 0.5) { l.ctx.shadowColor = 'rgba(239, 68, 68, 0.6)'; l.ctx.shadowBlur = agent.stress * 6; }
    l.ctx.beginPath(); l.ctx.arc(cx, cy, r, 0, Math.PI * 2); l.ctx.fill();
    l.ctx.shadowColor = 'transparent'; l.ctx.shadowBlur = 0;

    if (agent.is_stationary) {
      l.ctx.strokeStyle = 'rgba(147, 197, 253, 0.6)'; l.ctx.lineWidth = 1.5;
      l.ctx.beginPath(); l.ctx.arc(cx, cy, r + 2, 0, Math.PI * 2); l.ctx.stroke();
    }

    if (showDirection && cellSize > 6) {
      const dirs = [-Math.PI / 2, Math.PI / 2, Math.PI, 0];
      const angle = dirs[agent.facing_direction] ?? Math.PI / 2;
      const ad = r + 2, al = Math.max(cellSize * 0.2, 2);
      const ax = cx + Math.cos(angle) * ad, ay = cy + Math.sin(angle) * ad;
      l.ctx.fillStyle = 'rgba(255, 255, 255, 0.6)';
      l.ctx.beginPath();
      l.ctx.moveTo(ax + Math.cos(angle) * al, ay + Math.sin(angle) * al);
      l.ctx.lineTo(ax + Math.cos(angle + 2.4) * al * 0.6, ay + Math.sin(angle + 2.4) * al * 0.6);
      l.ctx.lineTo(ax + Math.cos(angle - 2.4) * al * 0.6, ay + Math.sin(angle - 2.4) * al * 0.6);
      l.ctx.closePath(); l.ctx.fill();
    }

    if (agent.is_stationary && cellSize > 10) {
      l.ctx.fillStyle = 'rgba(147, 197, 253, 0.8)';
      l.ctx.font = `${Math.max(8, cellSize * 0.25)}px monospace`;
      l.ctx.textAlign = 'center'; l.ctx.textBaseline = 'bottom';
      l.ctx.fillText(zzzLabel, cx, cy - r - 3);
    }

    if (agent.id === tracked) {
      l.ctx.strokeStyle = '#facc15'; l.ctx.lineWidth = 2;
      l.ctx.beginPath(); l.ctx.arc(cx, cy, r + 3, 0, Math.PI * 2); l.ctx.stroke();
    }

    if (cellSize > 12) {
      l.ctx.fillStyle = '#fff';
      l.ctx.font = `${Math.max(9, cellSize * 0.3)}px monospace`;
      l.ctx.textAlign = 'center'; l.ctx.textBaseline = 'middle';
      l.ctx.fillText(String(agent.id), cx, cy);
    }
  }
}

export function drawFloatingTexts(l: LayerCtx, texts: { id: number; x: number; y: number; text: string; color: string; startTime: number }[]) {
  const now = performance.now();
  const remaining = texts.filter(ft => now - ft.startTime < 1200);
  for (const ft of remaining) {
    const progress = (now - ft.startTime) / 1200;
    const alpha = 1 - progress;
    const fy = ft.y * l.cellSize - progress * l.cellSize * 1.5;
    l.ctx.globalAlpha = alpha;
    l.ctx.fillStyle = ft.color;
    l.ctx.font = `bold ${Math.max(10, l.cellSize * 0.5)}px monospace`;
    l.ctx.textAlign = 'center'; l.ctx.textBaseline = 'bottom';
    l.ctx.fillText(ft.text, ft.x * l.cellSize + l.cellSize / 2, fy);
  }
  l.ctx.globalAlpha = 1;
  // Mutate in place to allow caller to clean up
  texts.length = remaining.length;
}

export function drawHud(l: LayerCtx, agents: any[], zoomPct: number, tracked: number | null, label: string) {
  l.ctx.fillStyle = 'rgba(255,255,255,0.4)';
  l.ctx.font = '10px monospace'; l.ctx.textAlign = 'left'; l.ctx.textBaseline = 'top';
  l.ctx.fillText(label, 8, 8);
}
