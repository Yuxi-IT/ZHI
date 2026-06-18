import { useRef, useState, useCallback } from 'react';
import { useThree, useFrame } from '@react-three/fiber';
import * as THREE from 'three';

interface Props {
  gridW: number;
  gridH: number;
  heightMap: number[];
  biome: number[];
  temperatureGrid: number[];
  nutrient: number[];
  heightScale: number;
  t: (key: string, params?: Record<string, string | number>) => string;
}

export interface HoverInfo {
  gx: number;
  gy: number;
  height: number;
  temp: number;
  biome: string;
  nutrient: number;
  screenX: number;
  screenY: number;
}

export function useTooltip3D({ gridW, gridH, heightMap, biome, temperatureGrid, nutrient, heightScale, t }: Props) {
  const raycaster = useRef(new THREE.Raycaster());
  const { camera, gl } = useThree();
  const [hover, setHover] = useState<HoverInfo | null>(null);

  const onPointerMove = useCallback(
    (e: THREE.Event) => {
      const p = (e as unknown as { point: THREE.Vector3 }).point;
      if (!p) return;

      // Convert world position to grid coords
      const gx = Math.floor(p.x);
      const gy = Math.floor(gridH - p.z);

      if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) {
        setHover(null);
        return;
      }

      const idx = gy * gridW + gx;
      const h = heightMap[idx] ?? 128;
      const temp = temperatureGrid[idx] ?? 0;

      const biomeNames = [
        t('map.biomeWater'), t('map.biomeRiverBank'), t('map.biomeDesert'),
        t('map.biomeGrassland'), t('map.biomeJungle'), t('map.biomeWetland'),
        t('map.biomeHighland'), t('map.biomeValley'),
      ];
      const bIdx = biome[idx] ?? 0;
      const biomeName = bIdx >= 0 && bIdx < biomeNames.length ? (biomeNames[bIdx] ?? '?') : '?';

      // Project to screen
      const worldPos = new THREE.Vector3(gx + 0.5, (h / 255) * heightScale + 1, gridH - gy - 0.5);
      worldPos.project(camera);
      const screenX = (worldPos.x * 0.5 + 0.5) * gl.domElement.clientWidth;
      const screenY = (-worldPos.y * 0.5 + 0.5) * gl.domElement.clientHeight;

      setHover({
        gx, gy, height: h, temp,
        biome: biomeName,
        nutrient: nutrient[idx] ?? 0,
        screenX, screenY,
      });
    },
    [gridW, gridH, heightMap, biome, temperatureGrid, nutrient, heightScale, t, camera, gl]
  );

  const onPointerOut = useCallback(() => setHover(null), []);

  return { hover, onPointerMove, onPointerOut };
}
