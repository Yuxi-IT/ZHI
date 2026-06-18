import { useState, useEffect, useCallback } from 'react';
import { useTheme } from '@heroui/react';
import { LaunchPage } from './LaunchPage';
import { GameDashboard } from './GameDashboard';
import { useT } from '../i18n/I18nContext';

function ThemeInit() {
  const { resolvedTheme } = useTheme('system');
  return null;
}

export function App() {
  const [activeWorld, setActiveWorld] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const { t } = useT();

  useEffect(() => {
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
      <div className="min-h-screen bg-background flex items-center justify-center">
        <p className="text-foreground-400 text-sm animate-pulse">{t('app.loading')}</p>
      </div>
    );
  }

  if (!activeWorld) {
    return (
      <>
        <ThemeInit />
        <LaunchPage onWorldStart={handleWorldStart} />
      </>
    );
  }

  return (
    <>
      <ThemeInit />
      <GameDashboard worldName={activeWorld} onStop={handleWorldStop} />
    </>
  );
}
