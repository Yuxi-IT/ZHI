import { useMemo, useRef, useEffect } from 'react';
import * as THREE from 'three';
import type { FoodTile } from '../types';

const STAGE_COLORS: [number, number, number][] = [
  [0.55, 0.35, 0.15],  // Seed — brown
  [0.4, 0.7, 0.25],    // Sprout — light green
  [0.15, 0.55, 0.1],   // Adult — deep green
  [0.45, 0.45, 0.35],  // Decay — grey
];

interface Props {
  food: FoodTile[];
  gridW: number;
  gridH: number;
  heightMap: number[];
  heightScale: number;
}

export function PlantMarkers({ food, gridW, gridH, heightMap, heightScale }: Props) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  const boxGeo = useMemo(() => new THREE.BoxGeometry(0.25, 0.25, 0.25), []);
  const mat = useMemo(() => new THREE.MeshStandardMaterial({ roughness: 0.7 }), []);

  useEffect(() => {
    const mesh = meshRef.current;
    if (!mesh) return;

    const count = food.length;
    if (count === 0) { mesh.count = 0; return; }
    mesh.count = count;

    const dummy = new THREE.Object3D();
    const color = new THREE.Color();

    for (let i = 0; i < count; i++) {
      const p = food[i]!;
      const gx = p.x + 0.5;
      const gz = gridH - p.y - 0.5;
      const idx = p.y * gridW + p.x;
      const h = (heightMap[idx] ?? 128) / 255 * heightScale + 0.15;

      dummy.position.set(gx, h, gz);

      const energyRatio = Math.min(p.energy / (p.max_energy || 20), 1);
      const scaleY = 0.15 + energyRatio * 0.85;
      const scaleXZ = 0.15 + energyRatio * 0.25;
      dummy.scale.set(scaleXZ, scaleY, scaleXZ);

      const c = STAGE_COLORS[Math.min(p.stage, 3)]!;
      color.setRGB(c[0], c[1], c[2]);

      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
      mesh.setColorAt(i, color);
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [food, heightMap, heightScale, gridW, gridH]);

  return (
    <instancedMesh ref={meshRef} args={[boxGeo, mat, food.length]}>
    </instancedMesh>
  );
}
