import { useMemo, useRef } from 'react';
import * as THREE from 'three';

const BIOME_COLORS: [number, number, number][] = [
  [0.1, 0.3, 0.8],   // Water
  [0.2, 0.6, 0.4],   // RiverBank
  [0.8, 0.7, 0.4],   // Desert
  [0.3, 0.7, 0.2],   // Grassland
  [0.1, 0.5, 0.1],   // Jungle
  [0.2, 0.5, 0.5],   // Wetland
  [0.5, 0.4, 0.3],   // Highland
  [0.4, 0.6, 0.2],   // Valley
];

/** Map 0-255 height to a topographic color (green→yellow→brown→grey→white) */
function heightColor(h: number): [number, number, number] {
  const t = h / 255;
  if (t < 0.2) return [0.15, 0.35 + t * 1.5, 0.15];
  if (t < 0.4) return [0.25 + (t - 0.2) * 1.5, 0.55 + (t - 0.2) * 0.5, 0.12];
  if (t < 0.6) return [0.55 + (t - 0.4) * 1.25, 0.65 - (t - 0.4) * 0.5, 0.08 + (t - 0.4) * 0.3];
  if (t < 0.8) return [0.8, 0.55 + (t - 0.6) * 1.25, 0.14 + (t - 0.6) * 1.5];
  return [0.8 + (t - 0.8) * 1, 0.8 + (t - 0.8) * 1, 0.44 + (t - 0.8) * 2.8];
}

function rockColor(h: number): [number, number, number] {
  const c = 0.2 + (h / 255) * 0.35;
  return [c * 1.1, c, c * 0.8];
}

interface Props {
  heightMap: number[];
  slope: number[];
  biome: number[];
  gridW: number;
  gridH: number;
  heightScale: number;
  showBiome: boolean;
}

/** Build vertical skirt geometry along the four edges to make terrain look like a solid volume. */
function buildSkirt(
  heights: number[],
  gridW: number,
  gridH: number,
  heightScale: number,
  slope: number[],
  showBiome: boolean,
  biome: number[],
): THREE.BufferGeometry | null {
  const hw = gridW / 2;
  const hh = gridH / 2;
  const verts: number[] = [];
  const colors: number[] = [];
  const indices: number[] = [];

  function addQuad(
    x1: number, z1: number, y1: number,
    x2: number, z2: number, y2: number,
    x3: number, z3: number, y3: number,
    x4: number, z4: number, y4: number,
    hRaw: number, idx: number,
  ) {
    const base = verts.length / 3;
    verts.push(x1, y1, z1, x2, y2, z2, x3, y3, z3, x4, y4, z4);

    let r: number, g: number, b: number;
    if (showBiome && idx < biome.length) {
      [r, g, b] = BIOME_COLORS[Math.min(biome[idx] ?? 0, BIOME_COLORS.length - 1)]!;
    } else {
      [r, g, b] = rockColor(hRaw);
    }
    const s = slope[idx] ?? 0;
    const dim = 1 - Math.min(s * 0.6, 0.4);
    colors.push(r * dim, g * dim, b * dim, r * dim, g * dim, b * dim, r * dim, g * dim, b * dim, r * dim, g * dim, b * dim);

    indices.push(base, base + 1, base + 2, base, base + 2, base + 3);
  }

  // North edge (z = +hh → gy = 0)
  for (let gx = 0; gx < gridW; gx++) {
    const idx = 0 * gridW + gx;
    const h = (heights[idx] ?? 128) / 255 * heightScale;
    const x1 = gx - hw, x2 = gx + 1 - hw;
    addQuad(x1, hh, 0, x2, hh, 0, x2, hh, h, x1, hh, h, heights[idx] ?? 128, idx);
  }
  // South edge (z = -hh → gy = gridH-1)
  for (let gx = 0; gx < gridW; gx++) {
    const idx = (gridH - 1) * gridW + gx;
    const h = (heights[idx] ?? 128) / 255 * heightScale;
    const x1 = gx - hw, x2 = gx + 1 - hw;
    addQuad(x2, -hh, 0, x1, -hh, 0, x1, -hh, h, x2, -hh, h, heights[idx] ?? 128, idx);
  }
  // West edge (x = -hw → gx = 0)
  for (let gy = 0; gy < gridH; gy++) {
    const idx = gy * gridW + 0;
    const h = (heights[idx] ?? 128) / 255 * heightScale;
    const z1 = hh - gy, z2 = hh - (gy + 1);
    addQuad(-hw, z2, 0, -hw, z1, 0, -hw, z1, h, -hw, z2, h, heights[idx] ?? 128, idx);
  }
  // East edge (x = +hw → gx = gridW-1)
  for (let gy = 0; gy < gridH; gy++) {
    const idx = gy * gridW + (gridW - 1);
    const h = (heights[idx] ?? 128) / 255 * heightScale;
    const z1 = hh - gy, z2 = hh - (gy + 1);
    addQuad(hw, z1, 0, hw, z2, 0, hw, z2, h, hw, z1, h, heights[idx] ?? 128, idx);
  }

  if (verts.length === 0) return null;
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.Float32BufferAttribute(verts, 3));
  geo.setAttribute('color', new THREE.Float32BufferAttribute(colors, 3));
  geo.setIndex(indices);
  geo.computeVertexNormals();
  return geo;
}

export function TerrainMesh({ heightMap, slope, biome, gridW, gridH, heightScale, showBiome }: Props) {
  const surfaceRef = useRef<THREE.Mesh>(null);

  const { surfaceGeom, skirtGeom } = useMemo(() => {
    // ── Terrain surface ──
    const geo = new THREE.PlaneGeometry(gridW, gridH, gridW, gridH);
    geo.rotateX(-Math.PI / 2);

    const posAttr = geo.getAttribute('position');
    const positions = posAttr.array as Float32Array;
    const colors = new Float32Array(positions.length);
    const totalVerts = posAttr.count;

    for (let i = 0; i < totalVerts; i++) {
      const vx = positions[i * 3]!;
      const vz = positions[i * 3 + 2]!;
      const gx = Math.round(vx + gridW / 2 - 0.5);
      const gy = Math.round(gridH / 2 - vz - 0.5);
      const idx = gy * gridW + gx;
      const hRaw = heightMap[idx] ?? 128;
      positions[i * 3 + 1] = (hRaw / 255) * heightScale;

      let r: number, g: number, b: number;
      if (showBiome && idx >= 0 && idx < biome.length) {
        const bi = Math.min(biome[idx] ?? 0, BIOME_COLORS.length - 1);
        [r, g, b] = BIOME_COLORS[bi]!;
      } else {
        [r, g, b] = heightColor(hRaw);
      }
      const s = slope[idx] ?? 0;
      const dim = 1 - Math.min(s * 0.6, 0.35);
      colors[i * 3] = r * dim;
      colors[i * 3 + 1] = g * dim;
      colors[i * 3 + 2] = b * dim;
    }
    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geo.computeVertexNormals();

    // ── Side skirts ──
    const skirt = buildSkirt(heightMap, gridW, gridH, heightScale, slope, showBiome, biome);

    return { surfaceGeom: geo, skirtGeom: skirt };
  }, [heightMap, slope, biome, gridW, gridH, heightScale, showBiome]);

  return (
    <group>
      <mesh ref={surfaceRef} geometry={surfaceGeom} receiveShadow castShadow>
        <meshStandardMaterial vertexColors roughness={0.8} metalness={0.05} side={THREE.DoubleSide} />
      </mesh>
      {skirtGeom && (
        <mesh geometry={skirtGeom} receiveShadow>
          <meshStandardMaterial vertexColors roughness={0.9} metalness={0.02} side={THREE.DoubleSide} />
        </mesh>
      )}
    </group>
  );
}
