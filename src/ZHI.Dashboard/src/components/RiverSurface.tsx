import { useEffect, useMemo, useRef } from 'react';
import { useFrame } from '@react-three/fiber';
import * as THREE from 'three';

interface Props {
  river: number[];
  riverFlow: number[];
  gridW: number;
  gridH: number;
  heightMap: number[];
  heightScale: number;
}

export function RiverSurface({ river, gridW, gridH, heightMap, heightScale }: Props) {
  const meshRef = useRef<THREE.InstancedMesh>(null);

  const waterCells = useMemo(() => {
    const cells: { x: number; y: number; layers: number; depth: number }[] = [];
    for (let gy = 0; gy < gridH; gy++) {
      for (let gx = 0; gx < gridW; gx++) {
        const idx = gy * gridW + gx;
        const r = river[idx] ?? 0;
        if (r > 0) {
          const hRaw = Number(heightMap[idx]) || 128;
          const columnHeight = 3 + Math.round((hRaw / 255) * heightScale);
          cells.push({ x: gx, y: gy, layers: columnHeight, depth: r });
        }
      }
    }
    return cells;
  }, [river, gridW, gridH, heightMap, heightScale]);

  const boxGeo = useMemo(() => new THREE.BoxGeometry(1, 0.4, 1), []);
  const mat = useMemo(
    () => new THREE.MeshStandardMaterial({
      color: new THREE.Color(0.2, 0.5, 0.9),
      roughness: 0.2,
      metalness: 0.1,
      transparent: true,
      opacity: 0.65,
    }),
    []
  );

  useEffect(() => {
    const mesh = meshRef.current;
    if (!mesh) return;

    const count = waterCells.length;
    if (count === 0) { mesh.count = 0; return; }
    mesh.count = count;

    const dummy = new THREE.Object3D();
    for (let i = 0; i < count; i++) {
      const c = waterCells[i]!;
      const worldX = c.x + 0.5;
      const worldZ = gridH - c.y - 0.5;
      dummy.position.set(worldX, c.layers + 0.2, worldZ);
      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
    }

    mesh.instanceMatrix.needsUpdate = true;
  }, [waterCells, gridH]);

  useFrame(({ clock }) => {
    const mesh = meshRef.current;
    if (!mesh || waterCells.length === 0) return;
    const t = clock.getElapsedTime();
    const dummy = new THREE.Object3D();
    for (let i = 0; i < waterCells.length; i++) {
      const c = waterCells[i]!;
      const worldX = c.x + 0.5;
      const worldZ = gridH - c.y - 0.5;
      const wave = Math.sin(worldX * 0.8 + t * 2.0) * 0.05 + Math.sin(worldZ * 0.6 + t * 1.5) * 0.04;
      dummy.position.set(worldX, c.layers + 0.2 + wave, worldZ);
      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
    }
    mesh.instanceMatrix.needsUpdate = true;
  });

  if (waterCells.length === 0) return null;

  return (
    <instancedMesh ref={meshRef} args={[boxGeo, mat, waterCells.length]} frustumCulled={false} renderOrder={1}>
    </instancedMesh>
  );
}
