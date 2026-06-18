import { useMemo } from 'react';
import { Canvas } from '@react-three/fiber';
import * as THREE from 'three';
import type { DrawData } from './WorldMap';
import { TerrainMesh } from './TerrainMesh';
import { RiverSurface } from './RiverSurface';
import { AgentMarkers } from './AgentMarkers';
import { PlantMarkers } from './PlantMarkers';
import { CorpseMarkers } from './CorpseMarkers';
import { MapControls3D } from './MapControls3D';
import { useT } from '../i18n/I18nContext';

interface Props {
  drawDataRef: React.RefObject<DrawData | null>;
  gridW: number;
  gridH: number;
  trackedAgent: number | null;
  showBiome: boolean;
}

/** Compute height scale adaptively: max relief ≈ 40% of grid width for dramatic 3D look */
function computeHeightScale(heightMap: number[], gridW: number): number {
  let minH = 255, maxH = 0;
  for (let i = 0; i < heightMap.length; i++) {
    const h = heightMap[i]!;
    if (h < minH) minH = h;
    if (h > maxH) maxH = h;
  }
  const range = maxH - minH;
  if (range < 20) return gridW * 0.3;
  // Map height range to ~40% of grid width
  return (gridW * 0.4) / (range / 255);
}

export function WorldMap3D({ drawDataRef, gridW, gridH, trackedAgent, showBiome }: Props) {
  const { t } = useT();
  const data = drawDataRef.current;
  const timeOfDay = data?.timeOfDay ?? 12;

  const heightScale = useMemo(
    () => (data ? computeHeightScale(data.heightMap, gridW) : 20),
    [data, gridW],
  );

  // Sun position based on time of day (0-24)
  const sunPosition = useMemo(() => {
    const azimuth = ((timeOfDay - 6) / 12) * Math.PI;
    const elevation = Math.sin((timeOfDay / 24) * Math.PI * 2) * 0.5 + 0.3;
    const dist = 60;
    return [
      Math.cos(azimuth) * Math.cos(elevation) * dist,
      Math.sin(elevation) * dist,
      Math.sin(azimuth) * Math.cos(elevation) * dist,
    ] as [number, number, number];
  }, [timeOfDay]);

  const brightness = useMemo(() => {
    const t2 = (timeOfDay - 6) / 12 * Math.PI;
    return Math.max(0.05, Math.sin(t2));
  }, [timeOfDay]);

  const sunColor = useMemo(() => {
    const t2 = brightness;
    return new THREE.Color(1, 0.6 + t2 * 0.4, 0.2 + t2 * 0.8);
  }, [brightness]);

  // Camera: start from a low side angle so height differences are visible
  const camTarget: [number, number, number] = [0, heightScale * 0.15, 0];
  const camPos: [number, number, number] = [gridW * 0.6, heightScale * 0.5, gridH * 0.8];

  if (!data || gridW <= 0 || gridH <= 0) {
    return <div className="flex-1 bg-zhi-bg flex items-center justify-center text-zhi-muted text-xs">{t('map.loading')}</div>;
  }

  return (
    <div className="flex-1 min-h-0 relative">
      <Canvas
        camera={{ position: camPos, fov: 50, near: 0.5, far: 300 }}
        gl={{ antialias: true, alpha: false }}
        style={{ background: `rgb(${Math.round(5 + brightness * 15)}, ${Math.round(5 + brightness * 15)}, ${Math.round(10 + brightness * 25)})` }}
        onCreated={({ gl, camera }) => {
          gl.setClearColor(new THREE.Color(
            0.02 + brightness * 0.06,
            0.02 + brightness * 0.06,
            0.04 + brightness * 0.1,
          ));
          camera.lookAt(...camTarget);
        }}
      >
        <ambientLight intensity={0.12 + brightness * 0.3} color={sunColor} />
        <directionalLight
          position={sunPosition}
          intensity={0.3 + brightness * 0.8}
          color={sunColor}
          castShadow={false}
        />
        {/* Fill light from opposite side to reduce harsh shadows */}
        <directionalLight
          position={[-sunPosition[0], sunPosition[1] * 0.3, -sunPosition[2]]}
          intensity={0.08 + brightness * 0.15}
          color={sunColor}
        />

        {/* Sea-level reference plane — dark blue base under the terrain */}
        <mesh position={[0, -0.1, 0]} rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
          <planeGeometry args={[gridW + 4, gridH + 4]} />
          <meshBasicMaterial color="#0a1628" />
        </mesh>

        <TerrainMesh
          heightMap={data.heightMap}
          slope={data.slope}
          biome={data.biome}
          gridW={gridW}
          gridH={gridH}
          heightScale={heightScale}
          showBiome={showBiome}
        />

        <RiverSurface
          river={data.river}
          riverFlow={data.riverFlow}
          gridW={gridW}
          gridH={gridH}
          heightMap={data.heightMap}
          heightScale={heightScale}
        />

        <AgentMarkers
          agents={data.agents}
          gridW={gridW}
          gridH={gridH}
          heightMap={data.heightMap}
          heightScale={heightScale}
          trackedAgent={trackedAgent}
        />

        <PlantMarkers
          food={data.food}
          gridW={gridW}
          gridH={gridH}
          heightMap={data.heightMap}
          heightScale={heightScale}
        />

        <CorpseMarkers
          corpses={data.corpses}
          gridW={gridW}
          gridH={gridH}
          heightMap={data.heightMap}
          heightScale={heightScale}
        />

        <MapControls3D heightScale={heightScale} camTarget={camTarget} />
      </Canvas>
    </div>
  );
}
