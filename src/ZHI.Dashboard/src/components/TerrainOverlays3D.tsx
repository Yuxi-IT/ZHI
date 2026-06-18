import { useMemo, useRef, useEffect } from 'react';
import * as THREE from 'three';
import type { DrawData } from './WorldMap';

interface Props {
  data: DrawData;
  gridW: number;
  gridH: number;
  heightScale: number;
  showScent: boolean;
  showFoodScent: boolean;
  showChemical: boolean;
  showTemp: boolean;
  showTerrain: boolean;
  showFlow: boolean;
  showGroundwater: boolean;
  showSurfaceWater: boolean;
  showNutrient: boolean;
  showPermeability: boolean;
  showPressure: boolean;
  showWind: boolean;
  showBiome: boolean;
  showDirection: boolean;
  showVision: boolean;
}

function tempColor(tp: number): [number, number, number] {
  if (tp < 5) return [59, 130, 246];
  if (tp < 15) { const s = (tp - 5) / 10; return [Math.round(59 + s * 89), Math.round(130 + s * 33), Math.round(246 + s * -16)]; }
  if (tp < 25) { const s = (tp - 15) / 10; return [Math.round(148 + s * -74), Math.round(163 + s * 59), Math.round(230 + s * -8)]; }
  if (tp < 35) { const s = (tp - 25) / 10; return [Math.round(74 + s * 165), Math.round(222 + s * -154), Math.round(222 + s * -154)]; }
  return [239, 68, 68];
}

const BIOME_COLORS_2D: [number, number, number][] = [
  [30, 64, 175], [34, 197, 94], [245, 158, 11], [132, 204, 22],
  [22, 101, 52], [14, 165, 233], [148, 163, 184], [251, 191, 36],
];

const BASE_HEIGHT = 3;
const OVERLAY_OFFSET = 0.2;

export function TerrainOverlays3D({
  data, gridW, gridH, heightScale,
  showScent, showFoodScent, showChemical, showTemp, showTerrain,
  showFlow, showGroundwater, showSurfaceWater, showNutrient,
  showPermeability, showPressure, showWind, showBiome,
  showDirection, showVision,
}: Props) {
  const arrowRef = useRef<THREE.InstancedMesh>(null);

  const activeOverlay = useMemo(() => {
    type OverlayKey = 'scent' | 'foodScent' | 'chemical' | 'temp' | 'terrain' | 'groundwater' | 'surfaceWater' | 'nutrient' | 'permeability' | 'pressure' | 'biome';
    const map: { key: OverlayKey; active: boolean; arr: number[] }[] = [
      { key: 'scent', active: showScent, arr: data.scent },
      { key: 'foodScent', active: showFoodScent, arr: data.foodScent },
      { key: 'chemical', active: showChemical, arr: data.chemicalField },
      { key: 'temp', active: showTemp, arr: data.temperatureGrid },
      { key: 'terrain', active: showTerrain, arr: data.heightMap },
      { key: 'groundwater', active: showGroundwater, arr: data.groundwater },
      { key: 'surfaceWater', active: showSurfaceWater, arr: data.surfaceWater },
      { key: 'nutrient', active: showNutrient, arr: data.nutrient },
      { key: 'permeability', active: showPermeability, arr: data.permeability },
      { key: 'pressure', active: showPressure, arr: data.pressure },
      { key: 'biome', active: showBiome, arr: data.biome },
    ];
    for (const m of map) {
      if (m.active && m.arr.length > 0) return m;
    }
    return null;
  }, [showScent, showFoodScent, showChemical, showTemp, showTerrain,
    showGroundwater, showSurfaceWater, showNutrient, showPermeability,
    showPressure, showBiome, data]);

  // Build overlay geometry with per-vertex color + alpha
  const overlayGeo = useMemo(() => {
    const vertsX = gridW + 1;
    const vertsZ = gridH + 1;
    const vertCount = vertsX * vertsZ;
    const positions = new Float32Array(vertCount * 3);
    const colors = new Float32Array(vertCount * 4); // RGBA

    const arr = activeOverlay?.arr ?? [];
    if (!activeOverlay) return null;

    for (let vz = 0; vz < vertsZ; vz++) {
      for (let vx = 0; vx < vertsX; vx++) {
        const vi = vz * vertsX + vx;
        const gx = Math.min(vx, gridW - 1);
        const gy = Math.min(gridH - 1 - Math.min(vz, gridH - 1), gridH - 1);
        const idx = gy * gridW + gx;

        const hRaw = (idx < data.heightMap.length) ? (Number(data.heightMap[idx]) || 128) : 128;
        const t = hRaw / 255;
        const y = BASE_HEIGHT + t * heightScale + OVERLAY_OFFSET;

        positions[vi * 3] = vx;
        positions[vi * 3 + 1] = y;
        positions[vi * 3 + 2] = vz;

        let r = 0.5, g = 0.5, b = 0.5, alpha = 0;

        switch (activeOverlay.key) {
          case 'scent': {
            const val = arr[idx] ?? 0;
            if (val > 0.01) { alpha = Math.min(val / 10, 0.6); [r, g, b] = [0.66, 0.33, 0.97]; }
            break;
          }
          case 'foodScent': {
            const val = arr[idx] ?? 0;
            if (val > 0.01) { alpha = Math.min(val / 10, 0.5); [r, g, b] = [0.13, 0.77, 0.37]; }
            break;
          }
          case 'chemical': {
            const val = arr[idx] ?? 0;
            if (val > 0.01) { alpha = Math.min(val, 0.5); [r, g, b] = [0.98, 0.8, 0.08]; }
            break;
          }
          case 'temp': {
            const [cr, cg, cb] = tempColor(arr[idx] ?? 20);
            alpha = 0.25; [r, g, b] = [cr / 255, cg / 255, cb / 255];
            break;
          }
          case 'terrain': {
            const v = (arr[idx] ?? 128) / 255;
            alpha = 0.15; [r, g, b] = [v, v, v];
            break;
          }
          case 'groundwater': {
            const val = arr[idx] ?? 0;
            if (val > 0.01) { alpha = Math.min(val, 0.5); [r, g, b] = [0.12, 0.25, 0.69]; }
            break;
          }
          case 'surfaceWater': {
            const val = arr[idx] ?? 0;
            if (val > 0.01) { alpha = Math.min(val / 3, 0.6); [r, g, b] = [0.23, 0.51, 0.96]; }
            break;
          }
          case 'nutrient': {
            const val = arr[idx] ?? 0;
            if (val > 0.01) { alpha = 0.35; const nt = Math.min(val / 10, 1); [r, g, b] = [(34 + nt * 146) / 255, (139 + nt * 50) / 255, (34 - nt * 10) / 255]; }
            break;
          }
          case 'permeability': {
            const val = arr[idx] ?? 0;
            const pt = Math.max(0, Math.min(1, (val - 0.2) / 1.8));
            alpha = 0.25; [r, g, b] = [(75 * (1 - pt) + 34 * pt) / 255, (85 * (1 - pt) + 197 * pt) / 255, (160 * (1 - pt) + 94 * pt) / 255];
            break;
          }
          case 'pressure': {
            const p = arr[idx] ?? 1000;
            const ct = Math.max(-1, Math.min(1, (p - 1000) / 30));
            alpha = 0.2;
            if (ct < 0) [r, g, b] = [(239 + ct * 50) / 255, (68 - ct * 30) / 255, (68 - ct * 30) / 255];
            else [r, g, b] = [(59 - ct * 30) / 255, (130 - ct * 50) / 255, (246 - ct * 50) / 255];
            break;
          }
          case 'biome': {
            const [cr, cg, cb] = BIOME_COLORS_2D[Math.min(arr[idx] ?? 0, 7)] ?? [128, 128, 128];
            alpha = 0.2; [r, g, b] = [cr / 255, cg / 255, cb / 255];
            break;
          }
        }

        colors[vi * 4] = r;
        colors[vi * 4 + 1] = g;
        colors[vi * 4 + 2] = b;
        colors[vi * 4 + 3] = alpha;
      }
    }

    const indices: number[] = [];
    for (let z = 0; z < gridH; z++) {
      for (let x = 0; x < gridW; x++) {
        const a = z * vertsX + x;
        const b = a + 1;
        const c = (z + 1) * vertsX + x;
        const d = c + 1;
        indices.push(a, c, b);
        indices.push(b, c, d);
      }
    }

    const geo = new THREE.BufferGeometry();
    geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 4));
    geo.setIndex(indices);
    geo.computeVertexNormals();
    return geo;
  }, [gridW, gridH, heightScale, activeOverlay, data.heightMap]);

  // Arrows
  const showArrows = showWind || showFlow || showDirection;
  const arrowData = useMemo(() => {
    if (!showArrows) return { count: 0, positions: [] as [number,number,number][], directions: [] as [number,number,number][], colors: [] as [number,number,number][] };

    const positions: [number, number, number][] = [];
    const directions: [number, number, number][] = [];
    const colors: [number, number, number][] = [];

    if (showFlow && data.riverFlow?.length > 0) {
      const dirs = [0, -Math.PI / 2, -Math.PI / 4, 0, Math.PI / 4, Math.PI / 2, 3 * Math.PI / 4, Math.PI, -3 * Math.PI / 4];
      for (let gy = 0; gy < gridH; gy += 2) {
        for (let gx = 0; gx < gridW; gx += 2) {
          const idx = gy * gridW + gx;
          const flow = data.riverFlow[idx] ?? 0;
          if (flow <= 0 || flow > 8) continue;
          const hRaw = (idx < data.heightMap.length) ? (Number(data.heightMap[idx]) || 128) : 128;
          const y = BASE_HEIGHT + (hRaw / 255) * heightScale + 0.5;
          const ang = dirs[flow]!;
          positions.push([gx + 0.5, y, gridH - gy - 0.5]);
          directions.push([Math.cos(ang), 0.2, -Math.sin(ang)]);
          colors.push([1, 1, 1]);
        }
      }
    }

    if (showWind && data.windX?.length > 0) {
      for (let gy = 0; gy < gridH; gy += 3) {
        for (let gx = 0; gx < gridW; gx += 3) {
          const idx = gy * gridW + gx;
          const wx = data.windX[idx] ?? 0;
          const wy = data.windY[idx] ?? 0;
          const mag = Math.sqrt(wx * wx + wy * wy);
          if (mag < 0.05) continue;
          const hRaw = (idx < data.heightMap.length) ? (Number(data.heightMap[idx]) || 128) : 128;
          const y = BASE_HEIGHT + (hRaw / 255) * heightScale + 2;
          positions.push([gx + 0.5, y, gridH - gy - 0.5]);
          directions.push([wx / mag, 0.1, -wy / mag]);
          colors.push([0.8, 0.9, 1]);
        }
      }
    }

    return { count: positions.length, positions, directions, colors };
  }, [showArrows, showWind, showFlow, data, gridW, gridH, heightScale]);

  const arrowGeo = useMemo(() => new THREE.ConeGeometry(0.2, 0.6, 4, 4), []);
  const arrowMat = useMemo(() => new THREE.MeshStandardMaterial({ roughness: 0.5, vertexColors: true }), []);

  useEffect(() => {
    const mesh = arrowRef.current;
    if (!mesh || arrowData.count === 0) { if (mesh) mesh.visible = false; return; }
    mesh.visible = true;
    mesh.count = arrowData.count;

    const dummy = new THREE.Object3D();
    const color = new THREE.Color();
    for (let i = 0; i < arrowData.count; i++) {
      const [px, py, pz] = arrowData.positions[i]!;
      const [dx, dy, dz] = arrowData.directions[i]!;
      const [cr, cg, cb] = arrowData.colors[i]!;

      dummy.position.set(px, py, pz);
      const up = new THREE.Vector3(0, 1, 0);
      const dir = new THREE.Vector3(dx, dy, dz).normalize();
      dummy.rotation.setFromQuaternion(new THREE.Quaternion().setFromUnitVectors(up, dir));
      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
      color.setRGB(cr, cg, cb);
      mesh.setColorAt(i, color);
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [arrowData]);

  if (!activeOverlay && !showArrows) return null;

  return (
    <>
      {activeOverlay && overlayGeo && (
        <mesh key={activeOverlay.key} geometry={overlayGeo} renderOrder={2} frustumCulled={false}>
          <shaderMaterial
            transparent
            depthWrite={false}
            vertexShader={`
              varying vec4 vColor;
              attribute vec4 color;
              void main() {
                vColor = color;
                gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
              }
            `}
            fragmentShader={`
              varying vec4 vColor;
              void main() {
                if (vColor.a < 0.005) discard;
                gl_FragColor = vec4(vColor.rgb, vColor.a);
              }
            `}
          />
        </mesh>
      )}
      {showArrows && arrowData.count > 0 && (
        <instancedMesh key={arrowData.count} ref={arrowRef} args={[arrowGeo, arrowMat, Math.max(1, arrowData.count)]} renderOrder={3} frustumCulled={false}>
        </instancedMesh>
      )}
    </>
  );
}
