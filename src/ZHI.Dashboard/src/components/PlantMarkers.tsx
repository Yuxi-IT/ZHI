import { useMemo, useRef, useEffect } from 'react';
import * as THREE from 'three';
import type { FoodTile } from '../types';

// [stage][species]: 0=Grass, 1=Bush, 2=Tree
const SPECIES_STAGE_COLORS: [number, number, number][][] = [
  [[0.55, 0.35, 0.15], [0.47, 0.32, 0.16], [0.39, 0.27, 0.14]], // Seed: Grass/Bush/Tree
  [[0.56, 0.93, 0.56], [0.39, 0.78, 0.31], [0.31, 0.63, 0.24]], // Sprout
  [[0.13, 0.77, 0.37], [0.08, 0.59, 0.24], [0.06, 0.47, 0.18]], // Adult
  [[0.50, 0.50, 0.50], [0.43, 0.39, 0.35], [0.35, 0.31, 0.27]], // Decay
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
      const hRaw = Number(heightMap[idx]) || 128;
      const columnHeight = 3 + Math.round((hRaw / 255) * heightScale);
      const h = columnHeight + 0.15;

      dummy.position.set(gx, h, gz);

      const energyRatio = Math.min(p.energy / (p.max_energy || 20), 1);
      const scaleY = 0.15 + energyRatio * 0.85;
      const scaleXZ = 0.15 + energyRatio * 0.25;
      dummy.scale.set(scaleXZ, scaleY, scaleXZ);

      const speciesIdx = Math.min((p as any).species ?? 0, 2);
      const c = SPECIES_STAGE_COLORS[Math.min(p.stage, 3)]![speciesIdx]!;
      color.setRGB(c[0], c[1], c[2]);

      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
      mesh.setColorAt(i, color);
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [food, heightMap, heightScale, gridW, gridH]);

  return (
    <instancedMesh ref={meshRef} args={[boxGeo, mat, food.length]} frustumCulled={false}>
    </instancedMesh>
  );
}
