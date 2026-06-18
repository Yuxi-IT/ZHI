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
  if (t < 0.2) return [0.15, 0.35 + t * 1.5, 0.15];       // deep green
  if (t < 0.4) return [0.25 + (t - 0.2) * 1.5, 0.55 + (t - 0.2) * 0.5, 0.12]; // green→yellow-green
  if (t < 0.6) return [0.55 + (t - 0.4) * 1.25, 0.65 - (t - 0.4) * 0.5, 0.08 + (t - 0.4) * 0.3]; // →brown
  if (t < 0.8) return [0.8, 0.55 + (t - 0.6) * 1.25, 0.14 + (t - 0.6) * 1.5];  // brown→grey
  return [0.8 + (t - 0.8) * 1, 0.8 + (t - 0.8) * 1, 0.44 + (t - 0.8) * 2.8];  // grey→white
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

export function TerrainMesh({ heightMap, slope, biome, gridW, gridH, heightScale, showBiome }: Props) {
  const meshRef = useRef<THREE.Mesh>(null);

  const geometry = useMemo(() => {
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
      const h = hRaw / 255;
      positions[i * 3 + 1] = h * heightScale;

      // Base color: biome or height-based topographic
      let r: number, g: number, b: number;
      if (showBiome && idx >= 0 && idx < biome.length) {
        const biomeIdx = Math.min(biome[idx] ?? 0, BIOME_COLORS.length - 1);
        [r, g, b] = BIOME_COLORS[biomeIdx]!;
      } else {
        [r, g, b] = heightColor(hRaw);
      }

      // Slope shading: darken steep areas for relief
      const s = slope[idx] ?? 0;
      const slopeDim = 1 - Math.min(s * 0.6, 0.35);
      colors[i * 3] = r * slopeDim;
      colors[i * 3 + 1] = g * slopeDim;
      colors[i * 3 + 2] = b * slopeDim;
    }

    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geo.computeVertexNormals();
    return geo;
  }, [heightMap, slope, biome, gridW, gridH, heightScale, showBiome]);

  return (
    <mesh ref={meshRef} geometry={geometry} receiveShadow>
      <meshStandardMaterial vertexColors roughness={0.85} metalness={0.05} />
    </mesh>
  );
}
