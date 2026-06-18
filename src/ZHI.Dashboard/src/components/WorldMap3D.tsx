import { useState, useEffect } from 'react';
import { Canvas } from '@react-three/fiber';
import { OrbitControls } from '@react-three/drei';
import * as THREE from 'three';
import type { DrawData } from './WorldMap';
import { TerrainMesh } from './TerrainMesh';
import { RiverSurface } from './RiverSurface';
import { AgentMarkers } from './AgentMarkers';
import { PlantMarkers } from './PlantMarkers';
import { CorpseMarkers } from './CorpseMarkers';
import { TerrainOverlays3D } from './TerrainOverlays3D';
import { useT } from '../i18n/I18nContext';

interface Props {
  drawDataRef: React.RefObject<DrawData | null>;
  gridW: number;
  gridH: number;
  trackedAgent: number | null;
  showBiome: boolean;
  showScent?: boolean;
  showFoodScent?: boolean;
  showDirection?: boolean;
  showVision?: boolean;
  showChemical?: boolean;
  showTemp?: boolean;
  showTerrain?: boolean;
  showFlow?: boolean;
  showGroundwater?: boolean;
  showSurfaceWater?: boolean;
  showNutrient?: boolean;
  showPermeability?: boolean;
  showPressure?: boolean;
  showWind?: boolean;
}

export function WorldMap3D({
  drawDataRef, gridW, gridH, trackedAgent, showBiome,
  showScent = false, showFoodScent = false, showDirection = false,
  showVision = false, showChemical = false, showTemp = false,
  showTerrain = false, showFlow = false, showGroundwater = false,
  showSurfaceWater = false, showNutrient = false, showPermeability = false,
  showPressure = false, showWind = false,
}: Props) {
  const { t } = useT();
  const [tick, setTick] = useState(0);

  useEffect(() => {
    const id = setInterval(() => setTick(t => t + 1), 500);
    return () => clearInterval(id);
  }, []);

  void tick;
  const data = drawDataRef.current;

  if (!data || gridW <= 0 || gridH <= 0) {
    return (
      <div className="flex-1 bg-zhi-bg flex items-center justify-center text-zhi-muted text-xs">
        {t('map.loading')}
      </div>
    );
  }

  const heightScale = 3;
  const cx = gridW * 0.5;
  const cz = gridH * 0.5;
  const camDist = Math.max(gridW, gridH) * 1.0;

  return (
    <div className="flex-1 min-h-0 relative">
      <div style={{ position: 'absolute', top: 0, left: 0, right: 0, bottom: 0 }}>
        <Canvas
          camera={{
            position: [cx + camDist * 0.5, camDist * 0.6, cz + camDist * 0.5],
            fov: 45,
            near: 0.05,
            far: 2000,
          }}
          gl={{ antialias: true }}
          onCreated={({ gl, camera }) => {
            gl.setClearColor(new THREE.Color('#111118'));
            camera.lookAt(cx, 0, cz);
          }}
        >
          <ambientLight intensity={1.2} />
          <directionalLight position={[cx, 60, cz + 20]} intensity={1.5} />
          <directionalLight position={[cx - 30, 40, cz - 30]} intensity={0.6} />

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

          <TerrainOverlays3D
            data={data}
            gridW={gridW}
            gridH={gridH}
            heightScale={heightScale}
            showScent={showScent}
            showFoodScent={showFoodScent}
            showChemical={showChemical}
            showTemp={showTemp}
            showTerrain={showTerrain}
            showFlow={showFlow}
            showGroundwater={showGroundwater}
            showSurfaceWater={showSurfaceWater}
            showNutrient={showNutrient}
            showPermeability={showPermeability}
            showPressure={showPressure}
            showWind={showWind}
            showBiome={showBiome}
            showDirection={showDirection}
            showVision={showVision}
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

          <OrbitControls
            makeDefault
            target={[cx, 0, cz]}
            minDistance={1}
            maxDistance={500}
            maxPolarAngle={Math.PI / 2 - 0.02}
            enableDamping
            dampingFactor={0.12}
            mouseButtons={{
              LEFT: THREE.MOUSE.PAN,
              MIDDLE: THREE.MOUSE.ROTATE,
              RIGHT: THREE.MOUSE.DOLLY,
            }}
          />
        </Canvas>
      </div>
    </div>
  );
}
