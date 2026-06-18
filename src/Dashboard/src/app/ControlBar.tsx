import { Button, Separator } from '@heroui/react';
import { Pause, Play, Stop } from '@gravity-ui/icons';
import { ThemeSwitcher } from '../components/ThemeSwitcher';

interface Props {
  worldName: string;
  generation: number;
  totalDeaths: number;
  aliveCount: number;
  agentCount: number;
  connected: boolean;
  onPause: () => void;
  onStop: () => void;
  paused: boolean;
  stopping: boolean;
}

export function ControlBar({
  worldName, generation, totalDeaths, aliveCount, agentCount,
  connected, onPause, onStop, paused, stopping,
}: Props) {
  return (
    <header className="flex items-center gap-3 px-5 py-2 border-b border-zhi-border shrink-0 bg-zhi-panel">
      <div className="flex items-center gap-2">
        <h1 className="text-sm font-normal tracking-[0.2em] text-zhi-text">ZHI</h1>
        <Separator orientation="vertical" className="h-3" />
        <span className="text-xs text-zhi-text font-medium truncate max-w-40">{worldName}</span>
      </div>

      <div className="flex items-center gap-1.5">
        <Button
          variant="ghost"
          size="sm"
          className="text-[10px] text-zhi-muted hover:text-zhi-text min-w-0 h-auto px-2 py-0.5"
          onPress={onPause}
          isDisabled={stopping}
        >
          {paused ? <Play className="size-3" /> : <Pause className="size-3" />}
          {paused ? 'Resume' : 'Pause'}
        </Button>
        <Button
          variant="ghost"
          size="sm"
          className="text-[10px] text-red-400 hover:text-red-300 min-w-0 h-auto px-2 py-0.5"
          onPress={onStop}
          isDisabled={stopping}
        >
          <Stop className="size-3" />
          {stopping ? 'Stopping...' : 'Stop'}
        </Button>
      </div>

      <div className="flex items-center gap-2 ml-auto text-[10px] text-zhi-muted">
        <ThemeSwitcher />
        <Separator orientation="vertical" className="h-3" />
        <span>Gen {generation}</span>
        <Separator orientation="vertical" className="h-3" />
        <span>Deaths {totalDeaths}</span>
        <Separator orientation="vertical" className="h-3" />
        <span>Alive {aliveCount}/{agentCount}</span>
        <Separator orientation="vertical" className="h-3" />
        {connected ? (
          <span className="inline-flex items-center gap-1 text-green-400">
            <span className="w-1.5 h-1.5 rounded-full bg-green-500" />
            live
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 text-red-400">
            <span className="w-1.5 h-1.5 rounded-full bg-red-500" />
            off
          </span>
        )}
      </div>
    </header>
  );
}
