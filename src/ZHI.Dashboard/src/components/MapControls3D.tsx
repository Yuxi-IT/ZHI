import { OrbitControls } from '@react-three/drei';
import { useThree } from '@react-three/fiber';
import { useEffect } from 'react';

export function MapControls3D() {
  const { camera, gl } = useThree();

  useEffect(() => {
    camera.position.set(32, 50, 64);
    camera.lookAt(31.5, 0, 31.5);
  }, [camera]);

  return (
    <OrbitControls
      makeDefault
      minPolarAngle={0.1}
      maxPolarAngle={Math.PI / 2 - 0.05}
      minDistance={10}
      maxDistance={120}
      enableDamping
      dampingFactor={0.08}
      mouseButtons={{
        LEFT: 2,   // PAN
        MIDDLE: 1, // ROTATE
        RIGHT: 0,  // ROTATE
      }}
    />
  );
}
