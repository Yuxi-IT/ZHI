import { Button } from '@heroui/react';

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
    <header className="flex items-center gap-4 px-5 py-2 border-b border-zhi-border shrink-0 bg-zhi-panel">
      <div className="flex items-center gap-2">
        <h1 className="text-sm font-normal tracking-[0.2em] text-zhi-text">ZHI</h1>
        <span className="text-zhi-muted text-xs">|</span>
        <span className="text-xs text-zhi-text font-medium truncate max-w-40">{worldName}</span>
      </div>

      <div className="flex items-center gap-0.5">
        <Button
          className="text-[10px] bg-zhi-border text-zhi-muted hover:text-zhi-text px-2 py-0.5 min-w-0 h-auto rounded border-0"
          onPress={onPause}
          isDisabled={stopping}
        >
          {paused ? '▶ Resume' : '⏸ Pause'}
        </Button>
        <Button
          className="text-[10px] bg-red-900/30 text-red-400 hover:text-red-300 px-2 py-0.5 min-w-0 h-auto rounded border-0"
          onPress={onStop}
          isDisabled={stopping}
        >
          {stopping ? 'Stopping...' : '⏹ Stop'}
        </Button>
      </div>

      <div className="flex items-center gap-2 ml-auto text-[10px] text-zhi-muted">
        <span>Gen {generation}</span>
        <span className="text-zhi-border">|</span>
        <span>Deaths {totalDeaths}</span>
        <span className="text-zhi-border">|</span>
        <span>Alive {aliveCount}/{agentCount}</span>
        <span className="text-zhi-border">|</span>
        <span className={`w-1.5 h-1.5 rounded-full ${connected ? 'bg-green-500' : 'bg-red-500'}`} />
        <span>{connected ? 'live' : 'off'}</span>
      </div>
    </header>
  );
}
