import { OrbitControls } from '@react-three/drei';
import { useThree } from '@react-three/fiber';
import { useEffect, useRef } from 'react';

interface Props {
  heightScale: number;
  camTarget: [number, number, number];
}

export function MapControls3D({ heightScale, camTarget }: Props) {
  const { camera } = useThree();
  const initRef = useRef(false);

  // Set initial camera position once, then OrbitControls takes over
  useEffect(() => {
    if (initRef.current) return;
    initRef.current = true;
    const hw = 32;
    const hh = 32;
    camera.position.set(hw * 0.6, heightScale * 0.5, hh * 0.8);
    camera.lookAt(...camTarget);
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <OrbitControls
      makeDefault
      target={camTarget}
      minPolarAngle={0.05}
      maxPolarAngle={Math.PI / 2 - 0.05}
      minDistance={8}
      maxDistance={200}
      enableDamping
      dampingFactor={0.08}
      mouseButtons={{
        LEFT: 2,   // PAN — 左键拖动平移
        MIDDLE: 0, // ROTATE — 中键拖动旋转
        RIGHT: 1,  // DOLLY — 右键拖动缩放
      }}
    />
  );
}
