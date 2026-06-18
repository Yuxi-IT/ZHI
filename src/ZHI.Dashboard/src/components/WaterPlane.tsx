import { useMemo, useRef } from 'react';
import * as THREE from 'three';

interface Props {
  river: number[];
  surfaceWater: number[];
  gridW: number;
  gridH: number;
  heightMap: number[];
  heightScale: number;
}

export function WaterPlane({ river, surfaceWater, gridW, gridH, heightMap, heightScale }: Props) {
  const meshRef = useRef<THREE.Mesh>(null);

  const geometry = useMemo(() => {
    let maxWaterH = 0;

    for (let i = 0; i < gridW * gridH; i++) {
      if ((river[i] ?? 0) > 0 || (surfaceWater[i] ?? 0) > 0.5) {
        const h = (heightMap[i] ?? 128) / 255 * heightScale;
        if (h > maxWaterH) maxWaterH = h;
      }
    }

    const geo = new THREE.PlaneGeometry(gridW, gridH, 1, 1);
    geo.rotateX(-Math.PI / 2);

    const posAttr = geo.getAttribute('position');
    const positions = posAttr.array as Float32Array;
    const totalVerts = posAttr.count;
    const alphas = new Float32Array(totalVerts);

    for (let i = 0; i < totalVerts; i++) {
      const vx = positions[i * 3]!;
      const vz = positions[i * 3 + 2]!;
      const gx = Math.round(vx + gridW / 2 - 0.5);
      const gy = Math.round(gridH / 2 - vz - 0.5);
      const idx = gy * gridW + gx;

      positions[i * 3 + 1] = maxWaterH + 0.15;

      if ((river[idx] ?? 0) > 0) {
        alphas[i] = 0.6;
      } else if ((surfaceWater[idx] ?? 0) > 0.5) {
        alphas[i] = 0.3;
      } else {
        alphas[i] = 0;
      }
    }

    geo.setAttribute('alpha', new THREE.BufferAttribute(alphas, 1));
    return geo;
  }, [river, surfaceWater, gridW, gridH, heightMap, heightScale]);

  return (
    <mesh ref={meshRef} geometry={geometry} renderOrder={1}>
      <shaderMaterial
        transparent
        depthWrite={false}
        vertexShader={`
          varying float vAlpha;
          attribute float alpha;
          void main() {
            gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
            vAlpha = alpha;
          }
        `}
        fragmentShader={`
          varying float vAlpha;
          void main() {
            gl_FragColor = vec4(0.12, 0.45, 0.85, vAlpha);
          }
        `}
      />
    </mesh>
  );
}
