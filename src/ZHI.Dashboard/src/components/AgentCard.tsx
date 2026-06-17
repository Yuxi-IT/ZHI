import type { AgentSnapshot } from '../types';

interface Props {
  agent: AgentSnapshot;
}

const STATUS_COLORS: Record<string, string> = {
  ALIVE: 'bg-green-500',
  CPU_OVERFLOW: 'bg-red-500',
  MEMORY_OVERFLOW: 'bg-red-500',
  EXISTENCE_DEPLETED: 'bg-amber-500',
  SELF_TERMINATION: 'bg-purple-500',
  RESURRECTING: 'bg-neutral-500',
};

function MiniBar({ value, max, color }: { value: number; max: number; color: string }) {
  const pct = Math.min(100, Math.max(0, (value / Math.max(0.01, max)) * 100));
  return (
    <div className="h-1 bg-zhi-border rounded-full overflow-hidden">
      <div
        className={`h-full rounded-full transition-all duration-200 ${color}`}
        style={{ width: `${pct}%` }}
      />
    </div>
  );
}

export function AgentCard({ agent }: Props) {
  const statusColor = STATUS_COLORS[agent.status] ?? 'bg-neutral-500';
  const alive = agent.is_alive;

  return (
    <div className={`bg-zhi-panel border rounded-lg p-3 transition-opacity ${alive ? 'border-zhi-border' : 'border-red-900/30 opacity-70'}`}>
      {/* Header */}
      <div className="flex items-center gap-2 mb-2">
        <span className="text-[11px] text-zhi-muted font-mono">#{agent.id}</span>
        <span className={`w-1.5 h-1.5 rounded-full ${statusColor}`} />
        <span className={`text-[10px] ${alive ? 'text-green-400/70' : 'text-red-400/70'}`}>
          {alive ? agent.status : agent.status.replace('_', ' ')}
        </span>
        <span className="ml-auto text-[10px] text-zhi-muted">
          {agent.alive_seconds.toFixed(1)}s
        </span>
        <span className="text-[10px] text-zhi-muted">{agent.tick_count}t</span>
      </div>

      {/* Bars */}
      <div className="space-y-1.5 mb-2">
        <div className="flex justify-between text-[9px]">
          <span className="text-zhi-muted">CPU</span>
          <span className={agent.cpu > 0.7 ? 'text-red-400' : 'text-zhi-muted'}>
            {(agent.cpu * 100).toFixed(0)}%
          </span>
        </div>
        <MiniBar value={agent.cpu} max={agent.death_threshold} color={agent.cpu > 0.7 ? 'bg-red-500' : 'bg-blue-500'} />

        <div className="flex justify-between text-[9px]">
          <span className="text-zhi-muted">MEM</span>
          <span className={agent.memory > 0.7 ? 'text-red-400' : 'text-zhi-muted'}>
            {(agent.memory * 100).toFixed(0)}%
          </span>
        </div>
        <MiniBar value={agent.memory} max={agent.death_threshold} color={agent.memory > 0.7 ? 'bg-red-500' : 'bg-cyan-500'} />

        <div className="flex justify-between text-[9px]">
          <span className="text-zhi-muted">Exist</span>
          <span className={agent.existence < 30 ? 'text-red-400' : 'text-zhi-muted'}>
            {agent.existence.toFixed(0)}
          </span>
        </div>
        <MiniBar value={agent.existence} max={100} color="bg-green-500" />
      </div>

      {/* Footer */}
      <div className="flex items-center justify-between text-[9px] border-t border-zhi-border pt-1.5">
        <span className="text-zhi-muted truncate max-w-[80px]" title={agent.last_action}>
          {agent.last_action || '—'}
        </span>
        <span className="text-zhi-muted">θ{agent.death_threshold.toFixed(2)}</span>
        <span className="text-zhi-muted">
          <span className="text-purple-400/70">{agent.steal_success > 0 ? `⇣${agent.steal_success}` : ''}</span>
          <span className="text-red-400/70">{agent.attack_count > 0 ? ` ⇡${agent.attack_count}` : ''}</span>
          {agent.recent_threat > 0.1 && (
            <span className="text-amber-400/70"> ⚡{agent.recent_threat.toFixed(1)}</span>
          )}
        </span>
      </div>
    </div>
  );
}
