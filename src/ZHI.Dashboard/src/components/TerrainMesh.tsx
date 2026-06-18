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

interface Props {
  heightMap: number[];
  biome: number[];
  gridW: number;
  gridH: number;
  heightScale: number;
  showBiome: boolean;
}

export function TerrainMesh({ heightMap, biome, gridW, gridH, heightScale, showBiome }: Props) {
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

      if (showBiome && idx >= 0 && idx < biome.length) {
        const b = Math.min(biome[idx] ?? 0, BIOME_COLORS.length - 1);
        colors[i * 3] = BIOME_COLORS[b]![0];
        colors[i * 3 + 1] = BIOME_COLORS[b]![1];
        colors[i * 3 + 2] = BIOME_COLORS[b]![2];
      } else {
        const c = 0.25 + h * 0.4;
        colors[i * 3] = c;
        colors[i * 3 + 1] = c;
        colors[i * 3 + 2] = c;
      }
    }

    geo.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geo.computeVertexNormals();
    return geo;
  }, [heightMap, biome, gridW, gridH, heightScale, showBiome]);

  return (
    <mesh ref={meshRef} geometry={geometry} receiveShadow>
      <meshStandardMaterial vertexColors roughness={0.85} metalness={0.05} />
    </mesh>
  );
}
