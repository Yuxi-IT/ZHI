import { useEffect, useMemo, useRef } from 'react';
import { useFrame } from '@react-three/fiber';
import * as THREE from 'three';

// RiverFlow direction byte → Y-axis rotation (radians) for arrow cone
// Points north after rotation → cone tip faces downstream
const FLOW_ROTATIONS: Record<number, number> = {
  1: Math.PI,            // N
  2: -Math.PI / 4,       // NE
  3: -Math.PI / 2,       // E
  4: -3 * Math.PI / 4,   // SE
  5: 0,                  // S
  6: 3 * Math.PI / 4,    // SW
  7: Math.PI / 2,        // W
  8: Math.PI / 4,        // NW
};

interface Props {
  river: number[];
  riverFlow: number[];
  gridW: number;
  gridH: number;
  heightMap: number[];
  heightScale: number;
}

/** Terrain-following river surface with animated water + flow direction arrows */
export function RiverSurface({ river, riverFlow, gridW, gridH, heightMap, heightScale }: Props) {
  const waterRef = useRef<THREE.Mesh>(null);
  const flowRef = useRef<THREE.InstancedMesh>(null);
  const coneGeom = useMemo(() => new THREE.ConeGeometry(0.15, 0.4, 4, 4), []);

  // Build terrain-following water surface geometry
  const waterGeom = useMemo(() => {
    const geo = new THREE.PlaneGeometry(gridW, gridH, gridW, gridH);
    geo.rotateX(-Math.PI / 2);

    const posAttr = geo.getAttribute('position');
    const positions = posAttr.array as Float32Array;
    const alphas = new Float32Array(posAttr.count);

    for (let i = 0; i < posAttr.count; i++) {
      const vx = positions[i * 3]!;
      const vz = positions[i * 3 + 2]!;
      const gx = Math.round(vx + gridW / 2 - 0.5);
      const gy = Math.round(gridH / 2 - vz - 0.5);
      const idx = gy * gridW + gx;

      const riv = river[idx] ?? 0;
      const hRaw = heightMap[idx] ?? 128;
      const terrainY = (hRaw / 255) * heightScale;

      if (riv > 0) {
        positions[i * 3 + 1] = terrainY + 0.25;
        alphas[i] = riv === 2 ? 0.55 : 0.35;
      } else {
        positions[i * 3 + 1] = terrainY - 0.05;
        alphas[i] = 0;
      }
    }

    geo.setAttribute('alpha', new THREE.BufferAttribute(alphas, 1));
    geo.computeVertexNormals();
    return geo;
  }, [river, gridW, gridH, heightMap, heightScale]);

  // Pre-compute arrow instance data
  const arrowData = useMemo(() => {
    const result: { pos: THREE.Vector3; rot: number }[] = [];
    for (let gx = 0; gx < gridW; gx++) {
      for (let gy = 0; gy < gridH; gy++) {
        const idx = gy * gridW + gx;
        const riv = river[idx] ?? 0;
        const flow = riverFlow[idx] ?? 0;
        if (riv === 0 || flow === 0) continue;

        const hRaw = heightMap[idx] ?? 128;
        result.push({
          pos: new THREE.Vector3(
            gx - gridW / 2 + 0.5,
            (hRaw / 255) * heightScale + 0.35,
            gridH / 2 - gy - 0.5,
          ),
          rot: FLOW_ROTATIONS[flow] ?? 0,
        });
      }
    }
    return result;
  }, [river, riverFlow, gridW, gridH, heightMap, heightScale]);

  // Set instance matrices imperatively
  useEffect(() => {
    if (!flowRef.current || arrowData.length === 0) return;
    const mesh = flowRef.current;
    mesh.count = arrowData.length;
    const dummy = new THREE.Matrix4();
    const quat = new THREE.Quaternion();

    for (let i = 0; i < arrowData.length; i++) {
      const { pos, rot } = arrowData[i]!;
      quat.setFromAxisAngle(new THREE.Vector3(0, 1, 0), rot);
      dummy.compose(pos, quat, new THREE.Vector3(0.12, 0.12, 0.18));
      mesh.setMatrixAt(i, dummy);
    }
    mesh.instanceMatrix.needsUpdate = true;
  }, [arrowData]);

  // Animate water surface wave
  useFrame(({ clock }) => {
    if (waterRef.current) {
      const mat = waterRef.current.material as THREE.ShaderMaterial;
      if (mat.uniforms) {
        (mat.uniforms as Record<string, THREE.IUniform>).uTime!.value = clock.getElapsedTime();
      }
    }
  });

  const hasWater = waterGeom.getAttribute('alpha').array.some((v: number) => v > 0);

  if (!hasWater) return null;

  return (
    <group>
      {/* Animated water surface */}
      <mesh ref={waterRef} geometry={waterGeom} renderOrder={1}>
        <shaderMaterial
          transparent
          depthWrite={false}
          uniforms={{ uTime: { value: 0 } }}
          vertexShader={`
            varying float vAlpha;
            varying vec3 vWorldPos;
            attribute float alpha;
            uniform float uTime;
            void main() {
              vec4 worldPos = modelMatrix * vec4(position, 1.0);
              vWorldPos = worldPos.xyz;
              float wave = sin(worldPos.x * 0.8 + uTime * 2.0) * cos(worldPos.z * 0.7 + uTime * 1.7) * 0.15;
              vec3 displaced = position + vec3(0.0, wave, 0.0);
              gl_Position = projectionMatrix * modelViewMatrix * vec4(displaced, 1.0);
              vAlpha = alpha;
            }
          `}
          fragmentShader={`
            varying float vAlpha;
            varying vec3 vWorldPos;
            uniform float uTime;
            void main() {
              float shimmer = sin(vWorldPos.x * 3.0 + uTime * 1.5) * 0.05 + 0.95;
              vec3 shallowColor = vec3(0.25, 0.55, 0.85);
              vec3 deepColor = vec3(0.08, 0.30, 0.60);
              float depthMix = clamp(vAlpha * 1.5, 0.0, 1.0);
              vec3 color = mix(shallowColor, deepColor, depthMix) * shimmer;
              gl_FragColor = vec4(color, vAlpha);
            }
          `}
        />
      </mesh>

      {/* Flow direction arrows (InstancedMesh cones) */}
      {arrowData.length > 0 && (
        <instancedMesh
          ref={flowRef}
          args={[coneGeom, undefined, arrowData.length]}
          renderOrder={2}
        >
          <meshBasicMaterial color="#a0d8ff" transparent opacity={0.7} depthWrite={false} />
        </instancedMesh>
      )}
    </group>
  );
}
