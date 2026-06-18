import { useState, useMemo } from 'react';
import { Card, Button, Badge } from '@heroui/react';
import { Pin, PinFill, Diamond, DiamondFill, CircleFill } from '@gravity-ui/icons';
import type { AgentSnapshot } from '../types';
import { useT } from '../i18n/I18nContext';

interface Props {
  agents: AgentSnapshot[];
  onTrack?: (id: number | null) => void;
  trackedId?: number | null;
  terrain?: number[];
  gridW?: number;
}

type SortMode = 'none' | 'hp-desc' | 'hp-asc';

export function AgentCardsPanel({ agents, onTrack, trackedId, terrain, gridW }: Props) {
  const { t } = useT();
  const [pinnedIds, setPinnedIds] = useState<Set<number>>(new Set());
  const [sortMode, setSortMode] = useState<SortMode>('none');

  const togglePin = (id: number) => {
    setPinnedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const aliveAgents = agents.filter(a => a.is_alive);
  const deadAgents = agents.filter(a => !a.is_alive);

  const sortedAlive = useMemo(() => {
    const pinned = aliveAgents.filter(a => pinnedIds.has(a.id));
    const rest = aliveAgents.filter(a => !pinnedIds.has(a.id));
    if (sortMode === 'hp-desc') rest.sort((a, b) => b.existence - a.existence);
    else if (sortMode === 'hp-asc') rest.sort((a, b) => a.existence - b.existence);
    return [...pinned, ...rest];
  }, [aliveAgents, pinnedIds, sortMode]);

  const cycleSort = () => {
    setSortMode(prev =>
      prev === 'none' ? 'hp-desc' : prev === 'hp-desc' ? 'hp-asc' : 'none'
    );
  };

  const sortLabel = sortMode === 'hp-desc' ? t('agents.sortHpDesc') : sortMode === 'hp-asc' ? t('agents.sortHpAsc') : t('agents.sort');
  const sortTitle = sortMode === 'none' ? t('agents.sortTitleNone') : sortMode === 'hp-desc' ? t('agents.sortTitleDesc') : t('agents.sortTitleAsc');

  return (
    <div className="h-full flex flex-col min-h-0">
      <div className="flex items-center gap-2 px-3 py-1.5 border-b border-zhi-border shrink-0">
        <span className="text-zhi-muted text-[10px]">
          {t('agents.alive')} {aliveAgents.length}/{agents.length}
        </span>
        {pinnedIds.size > 0 && (
          <Badge variant="secondary" className="text-[9px]">{t('agents.pin')}:{pinnedIds.size}</Badge>
        )}
        <div className="ml-auto flex items-center gap-1">
          <Button
            variant={sortMode !== 'none' ? 'secondary' : 'ghost'}
            size="sm"
            className="text-[9px] min-w-0 h-auto px-1.5 py-0.5"
            onPress={cycleSort}
          >
            {sortLabel}
          </Button>
          {pinnedIds.size > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="text-[9px] text-zhi-muted hover:text-zhi-text min-w-0 h-auto px-1.5 py-0.5"
              onPress={() => setPinnedIds(new Set())}
            >
              {t('agents.unpinAll')}
            </Button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-2 space-y-1.5">
        {sortedAlive.map(agent => (
          <AgentCard
            key={agent.id}
            agent={agent}
            pinned={pinnedIds.has(agent.id)}
            tracked={agent.id === trackedId}
            onTogglePin={() => togglePin(agent.id)}
            onTrack={() => onTrack?.(agent.id === trackedId ? null : agent.id)}
            terrain={terrain}
            gridW={gridW}
          />
        ))}

        {deadAgents.length > 0 && (
          <div className="pt-2 border-t border-zhi-border">
            <div className="text-zhi-muted text-[10px] px-1 mb-1">{t('agents.dead')} ({deadAgents.length})</div>
            {deadAgents.slice(-10).map(agent => (
              <div
                key={agent.id}
                className="rounded border border-zhi-border/50 bg-zhi-bg/30 p-1.5 text-[10px] mb-1 opacity-60"
              >
                <span className="text-zhi-muted">#{agent.id}</span>
                <span className="text-zhi-muted ml-2">{agent.status}</span>
                <span className="text-zhi-muted ml-2">{agent.tick_count}t</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function AgentCard({
  agent, pinned, tracked, onTogglePin, onTrack, terrain, gridW,
}: {
  agent: AgentSnapshot;
  pinned: boolean;
  tracked: boolean;
  onTogglePin: () => void;
  onTrack: () => void;
  terrain?: number[];
  gridW?: number;
}) {
  const { t } = useT();
  const hp = Math.max(0, Math.min(1, agent.existence / 100));
  const hpColor = `hsl(${hp * 120}, 70%, 50%)`;
  const isTracked = tracked;
  const isPinned = pinned;

  return (
    <Card
      variant="secondary"
      className={`p-2 text-[10px] transition-colors ${
        isTracked
          ? 'border-yellow-500/50 bg-yellow-900/10'
          : isPinned
          ? 'border-zhi-muted bg-zhi-panel/70'
          : 'border-zhi-border bg-zhi-panel/50'
      }`}
    >
      <Card.Content className="p-0">
        <div className="flex items-center justify-between mb-1">
          <div className="flex items-center gap-1.5">
            <Button
              isIconOnly
              variant="ghost"
              size="sm"
              className={`min-w-0 w-4 h-4 ${isPinned ? 'text-yellow-500' : 'text-zhi-muted hover:text-zhi-text'}`}
              aria-label={isPinned ? t('agents.unpin') : t('agents.pinToTop')}
              onPress={onTogglePin}
            >
              {isPinned ? <PinFill className="size-2.5" /> : <Pin className="size-2.5" />}
            </Button>
            <span className="text-zhi-text font-medium">#{agent.id}</span>
            {agent.respawn_count > 0 && (
              <span className="text-zhi-muted text-[9px]">G{agent.respawn_count}</span>
            )}
          </div>
          <div className="flex items-center gap-1.5">
            <Button
              isIconOnly
              variant="ghost"
              size="sm"
              className={`min-w-0 w-4 h-4 ${isTracked ? 'text-yellow-400' : 'text-zhi-muted hover:text-zhi-text'}`}
              aria-label={isTracked ? t('agents.stopTracking') : t('agents.trackOnMap')}
              onPress={onTrack}
            >
              {isTracked ? <DiamondFill className="size-2.5" /> : <Diamond className="size-2.5" />}
            </Button>
            <CircleFill className="size-2" style={{ color: hpColor }} />
          </div>
        </div>

        <div className="text-zhi-muted space-y-0.5">
          <StatRow label={t('agents.hp')} value={agent.existence.toFixed(1)} />
          <StatRow label={t('agents.stress')} value={agent.stress.toFixed(2)} warn={agent.stress > 1} />
          <StatRow label={t('agents.hunger')} value={agent.hunger.toFixed(1)} warn={agent.hunger < 20} />
          <StatRow label={t('agents.thirst')} value={agent.thirst.toFixed(1)} accent="cyan" warn={agent.thirst < 20} />
          <StatRow label={t('agents.stamina')} value={agent.stamina.toFixed(1)} accent="yellow" warn={agent.stamina < 10} />
          <StatRow label="BTemp" value={`${agent.body_temperature.toFixed(1)}°C`} accent="blue" warn={agent.body_temperature < 10} hot={agent.body_temperature > 35} />
          <StatRow label={t('agents.age')} value={`${agent.tick_count}t`} />
          <StatRow label={t('agents.action')} value={actionLabel(agent, terrain, gridW, t)} />
          <StatRow label={t('agents.pos')} value={`(${agent.x},${agent.y})`} />
          <div className="flex gap-3 mt-0.5 text-zhi-muted">
            <span title="Food">F:{agent.food_eat_count}</span>
            <span title="BigFood">B:{agent.bigfood_eat_count}</span>
            <span title="Corpse">C:{agent.corpse_eat_count}</span>
            <span title="Attacks">A:{agent.attack_count}</span>
            <span title="Signals">S:{agent.signal_count}</span>
            <span title="Push">P:{agent.push_count}</span>
            <span title="Terraform">T:{agent.terraform_count}</span>
          </div>
        </div>
      </Card.Content>
    </Card>
  );
}

function StatRow({ label, value, warn, hot, accent }: {
  label: string; value: string; warn?: boolean; hot?: boolean; accent?: string;
}) {
  let valColor = 'text-zhi-text';
  if (warn && accent === 'cyan') valColor = 'text-cyan-400';
  else if (warn && accent === 'yellow') valColor = 'text-yellow-400';
  else if (warn && accent === 'blue') valColor = 'text-blue-400';
  else if (warn) valColor = accent === 'cyan' ? 'text-cyan-400' : 'text-orange-400';
  if (hot) valColor = 'text-red-400';

  return (
    <div className="flex justify-between">
      <span>{label}</span>
      <span className={valColor}>{value}</span>
    </div>
  );
}

function actionLabel(
  agent: AgentSnapshot,
  terrain: number[] | undefined,
  gridW: number | undefined,
  t: (key: string, params?: Record<string, string | number>) => string,
): string {
  const ttype = terrain && gridW ? terrain[agent.y * gridW + agent.x] : 0;
  const badges: string[] = [];
  if (agent.is_stationary) badges.push('💤');
  if (ttype === 1) badges.push('🕳️');
  if (ttype === 2) badges.push('🗼');
  if (agent.is_eating) badges.push('🍖');
  const prefix = badges.length ? badges.join(' ') + ' ' : '';
  return prefix + (agent.last_action || t('agents.none'));
}
