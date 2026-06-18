import { useState, useEffect, useCallback } from 'react';
import { LaunchPage } from './LaunchPage';
import { GameDashboard } from './GameDashboard';

export function App() {
  const [activeWorld, setActiveWorld] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Check if there's already a running world
    fetch('/api/worlds')
      .then((r) => r.json())
      .then((worlds: { name: string; status: string }[]) => {
        const running = worlds.find((w) => w.status === 'running');
        if (running) setActiveWorld(running.name);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleWorldStart = useCallback((name: string) => {
    setActiveWorld(name);
  }, []);

  const handleWorldStop = useCallback(() => {
    setActiveWorld(null);
  }, []);

  if (loading) {
    return (
      <div className="min-h-screen bg-zhi-bg flex items-center justify-center">
        <p className="text-zhi-muted text-sm animate-pulse">Loading...</p>
      </div>
    );
  }

  if (!activeWorld) {
    return <LaunchPage onWorldStart={handleWorldStart} />;
  }

  return <GameDashboard worldName={activeWorld} onStop={handleWorldStop} />;
}
