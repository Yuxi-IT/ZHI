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

export function WorldMap3D({ drawDataRef, gridW, gridH, trackedAgent, showBiome }: Props) {
  const { t } = useT();
  const data = drawDataRef.current;

  const heightScale = 8;
  const timeOfDay = data?.timeOfDay ?? 12;

  // Sun position based on time of day (0-24)
  const sunPosition = useMemo(() => {
    const azimuth = ((timeOfDay - 6) / 12) * Math.PI; // -PI/2 at 0h, 0 at 6h, PI/2 at 12h, PI at 18h
    const elevation = Math.sin((timeOfDay / 24) * Math.PI * 2) * 0.5 + 0.3; // 0.3 to 0.8
    const dist = 40;
    return [
      Math.cos(azimuth) * Math.cos(elevation) * dist,
      Math.sin(elevation) * dist,
      Math.sin(azimuth) * Math.cos(elevation) * dist,
    ] as [number, number, number];
  }, [timeOfDay]);

  const brightness = useMemo(() => {
    // Peak at noon (12), minimum at midnight (0/24)
    const t2 = (timeOfDay - 6) / 12 * Math.PI;
    return Math.max(0.05, Math.sin(t2));
  }, [timeOfDay]);

  const sunColor = useMemo(() => {
    const t2 = brightness;
    // Night: warm orange (2000K), Day: cool white (5500K)
    const r = 1;
    const g = 0.6 + t2 * 0.4;
    const b = 0.2 + t2 * 0.8;
    return new THREE.Color(r, g, b);
  }, [brightness]);

  if (!data || gridW <= 0 || gridH <= 0) {
    return <div className="flex-1 bg-zhi-bg flex items-center justify-center text-zhi-muted text-xs">{t('map.loading')}</div>;
  }

  return (
    <div className="flex-1 min-h-0 relative">
      <Canvas
        camera={{ position: [32, 50, 64], fov: 45, near: 1, far: 200 }}
        gl={{ antialias: true, alpha: false }}
        style={{ background: `rgb(${Math.round(5 + brightness * 15)}, ${Math.round(5 + brightness * 15)}, ${Math.round(10 + brightness * 25)})` }}
        onCreated={({ gl }) => {
          gl.setClearColor(new THREE.Color(
            0.02 + brightness * 0.06,
            0.02 + brightness * 0.06,
            0.04 + brightness * 0.1
          ));
        }}
      >
        <ambientLight intensity={0.15 + brightness * 0.3} color={sunColor} />
        <directionalLight
          position={sunPosition}
          intensity={0.2 + brightness * 0.8}
          color={sunColor}
          castShadow={false}
        />

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

        <MapControls3D />
      </Canvas>
    </div>
  );
}
