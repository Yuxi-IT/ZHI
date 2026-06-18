import { useMemo, useRef, useEffect } from 'react';
import * as THREE from 'three';

const BIOME_COLORS: [number, number, number][] = [
  [0.1, 0.3, 0.8],
  [0.2, 0.6, 0.4],
  [0.8, 0.7, 0.4],
  [0.4, 0.75, 0.3],
  [0.15, 0.55, 0.15],
  [0.25, 0.55, 0.5],
  [0.6, 0.5, 0.35],
  [0.5, 0.7, 0.3],
];

function heightColor(t: number): [number, number, number] {
  if (t < 0.25) return [0.35, 0.6 + t * 0.6, 0.3];
  if (t < 0.5) return [0.5 + (t - 0.25) * 1.0, 0.75, 0.25];
  if (t < 0.75) return [0.8, 0.7 - (t - 0.5) * 0.3, 0.35];
  return [0.9, 0.88, 0.75];
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

const BASE_HEIGHT = 3;

export function TerrainMesh({ heightMap, slope, biome, gridW, gridH, heightScale, showBiome }: Props) {
  const surfaceRef = useRef<THREE.Mesh>(null);

  const { geometry, baseGeo } = useMemo(() => {
    const geo = new THREE.PlaneGeometry(gridW, gridH, gridW, gridH);
    geo.rotateX(-Math.PI / 2);
    geo.translate(gridW / 2, 0, gridH / 2);

    const base = new THREE.BoxGeometry(gridW, BASE_HEIGHT, gridH);
    base.translate(gridW / 2, BASE_HEIGHT / 2, gridH / 2);

    return { geometry: geo, baseGeo: base };
  }, [gridW, gridH]);

  useEffect(() => {
    if (!surfaceRef.current || heightMap.length === 0) return;

    const geo = surfaceRef.current.geometry;
    const pos = geo.attributes.position!;
    const colorAttr = new Float32Array(pos.count * 3);

    for (let i = 0; i < pos.count; i++) {
      const vx = pos.getX(i);
      const vz = pos.getZ(i);

      const gx = Math.min(Math.max(Math.floor(vx), 0), gridW - 1);
      const gy = Math.min(Math.max(Math.floor(gridH - vz), 0), gridH - 1);
      const idx = gy * gridW + gx;

      const hRaw = (idx < heightMap.length) ? (Number(heightMap[idx]) || 128) : 128;
      const t = hRaw / 255;
      const y = BASE_HEIGHT + t * heightScale;

      pos.setY(i, y);

      let r: number, g: number, b: number;
      if (showBiome && idx < biome.length) {
        const bi = Math.min(Number(biome[idx]) || 0, BIOME_COLORS.length - 1);
        [r, g, b] = BIOME_COLORS[bi]!;
      } else {
        [r, g, b] = heightColor(t);
      }
      colorAttr[i * 3] = r;
      colorAttr[i * 3 + 1] = g;
      colorAttr[i * 3 + 2] = b;
    }

    pos.needsUpdate = true;
    geo.setAttribute('color', new THREE.BufferAttribute(colorAttr, 3));
    geo.computeVertexNormals();
  }, [heightMap, biome, gridW, gridH, heightScale, showBiome]);

  if (gridW <= 0 || gridH <= 0) return null;

  return (
    <group>
      {/* Solid base block */}
      <mesh geometry={baseGeo} frustumCulled={false}>
        <meshStandardMaterial color="#5a5040" roughness={0.9} />
      </mesh>

      {/* Terrain surface */}
      <mesh ref={surfaceRef} geometry={geometry} frustumCulled={false} receiveShadow>
        <meshStandardMaterial vertexColors roughness={0.75} metalness={0.05} flatShading />
      </mesh>
    </group>
  );
}
