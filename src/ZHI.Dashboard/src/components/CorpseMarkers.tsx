import { useMemo, useRef, useEffect } from 'react';
import * as THREE from 'three';
import type { CorpseTile } from '../types';

interface Props {
  corpses: CorpseTile[];
  gridW: number;
  gridH: number;
  heightMap: number[];
  heightScale: number;
}

export function CorpseMarkers({ corpses, gridW, gridH, heightMap, heightScale }: Props) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  const boxGeo = useMemo(() => new THREE.BoxGeometry(0.5, 0.15, 0.5), []);
  const mat = useMemo(
    () => new THREE.MeshStandardMaterial({ roughness: 0.9, transparent: true, opacity: 0.7 }),
    []
  );

  useEffect(() => {
    const mesh = meshRef.current;
    if (!mesh) return;

    const count = corpses.length;
    if (count === 0) { mesh.count = 0; return; }
    mesh.count = count;

    const dummy = new THREE.Object3D();
    const color = new THREE.Color();

    for (let i = 0; i < count; i++) {
      const c = corpses[i]!;
      const gx = c.x + 0.5;
      const gz = gridH - c.y - 0.5;
      const idx = c.y * gridW + c.x;
      const hRaw = Number(heightMap[idx]) || 128;
      const columnHeight = 3 + Math.round((hRaw / 255) * heightScale);
      const h = columnHeight + 0.08;

      dummy.position.set(gx, h, gz);

      dummy.scale.setScalar(0.5 + Math.min(c.energy / 30, 1) * 0.5);

      color.setRGB(0.35, 0.3, 0.25);

      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
      mesh.setColorAt(i, color);
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [corpses, heightMap, heightScale, gridW, gridH]);

  return (
    <instancedMesh ref={meshRef} args={[boxGeo, mat, corpses.length]} frustumCulled={false}>
    </instancedMesh>
  );
}
