import { useMemo, useRef, useEffect } from 'react';
import * as THREE from 'three';
import type { AgentSnapshot } from '../types';

interface Props {
  agents: AgentSnapshot[];
  gridW: number;
  gridH: number;
  heightMap: number[];
  heightScale: number;
  trackedAgent: number | null;
}

export function AgentMarkers({ agents, gridW, gridH, heightMap, heightScale, trackedAgent }: Props) {
  const meshRef = useRef<THREE.InstancedMesh>(null);
  const ringRef = useRef<THREE.Mesh>(null);

  const aliveAgents = useMemo(
    () => agents.filter(a => a.is_alive),
    [agents]
  );

  const sphereGeo = useMemo(() => new THREE.SphereGeometry(0.4, 8, 6), []);
  const mat = useMemo(() => new THREE.MeshStandardMaterial({ roughness: 0.4, metalness: 0.1 }), []);

  useEffect(() => {
    const mesh = meshRef.current;
    if (!mesh) return;

    const count = aliveAgents.length;
    if (count === 0) { mesh.count = 0; return; }
    mesh.count = count;

    const dummy = new THREE.Object3D();
    const color = new THREE.Color();

    for (let i = 0; i < count; i++) {
      const a = aliveAgents[i]!;
      const gx = a.x + 0.5;
      const gz = gridH - a.y - 0.5;
      const idx = a.y * gridW + a.x;
      const h = (heightMap[idx] ?? 128) / 255 * heightScale + 0.5;

      dummy.position.set(gx, h, gz);
      const t = Math.min(a.energy / 100, 1);
      color.setRGB(1 - t, t, 0);
      dummy.scale.setScalar(0.6 + t * 0.8);
      dummy.updateMatrix();
      mesh.setMatrixAt(i, dummy.matrix);
      mesh.setColorAt(i, color);
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;

    // Update tracking ring
    const ring = ringRef.current;
    if (ring) {
      if (trackedAgent !== null) {
        const target = aliveAgents.find(a => a.id === trackedAgent);
        if (target) {
          const gx = target.x + 0.5;
          const gz = gridH - target.y - 0.5;
          const idx = target.y * gridW + target.x;
          const h = (heightMap[idx] ?? 128) / 255 * heightScale + 0.5;
          ring.position.set(gx, h, gz);
          ring.visible = true;
        } else {
          ring.visible = false;
        }
      } else {
        ring.visible = false;
      }
    }
  }, [aliveAgents, heightMap, heightScale, gridW, gridH, trackedAgent]);

  return (
    <>
      <instancedMesh ref={meshRef} args={[sphereGeo, mat, aliveAgents.length]}>
      </instancedMesh>
      <mesh ref={ringRef} visible={trackedAgent !== null} renderOrder={2}>
        <ringGeometry args={[0.8, 1.0, 32]} />
        <meshBasicMaterial color="#ffff00" side={THREE.DoubleSide} depthTest={false} />
      </mesh>
    </>
  );
}
