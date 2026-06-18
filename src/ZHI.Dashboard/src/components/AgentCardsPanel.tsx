import { useState, useMemo, memo } from 'react';
import { Card, Button } from '@heroui/react';
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

export const AgentCardsPanel = memo(function AgentCardsPanel({ agents, onTrack, trackedId, terrain, gridW }: Props) {
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

  return (
    <div className="h-full flex flex-col min-h-0">
      <div className="flex items-center gap-2 px-3 py-1.5 border-b border-zhi-border shrink-0">
        <span className="text-zhi-muted text-[11px]">
          {t('agents.alive')} {aliveAgents.length}/{agents.length}
        </span>
        <div className="ml-auto flex items-center gap-1">
          <Button
            variant={sortMode !== 'none' ? 'secondary' : 'ghost'}
            size="sm"
            className="text-[10px] min-w-0 h-auto px-1.5 py-0.5"
            onPress={cycleSort}
          >
            {sortLabel}
          </Button>
          {pinnedIds.size > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="text-[10px] text-zhi-muted hover:text-zhi-text min-w-0 h-auto px-1.5 py-0.5"
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
            <div className="text-zhi-muted text-[11px] px-1 mb-1">{t('agents.dead')} ({deadAgents.length})</div>
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
});

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
  const warnHunger = agent.hunger < 20;
  const warnThirst = agent.thirst < 20;
  const warnStamina = agent.stamina < 10;
  const warnCold = agent.body_temperature < 10;
  const warnHot = agent.body_temperature > 35;

  return (
    <Card
      variant="secondary"
      className={`p-1.5 text-[10px] transition-colors ${
        tracked
          ? 'border-yellow-500/50 bg-yellow-900/10'
          : pinned
          ? 'border-zhi-muted bg-zhi-panel/70'
          : 'border-zhi-border bg-zhi-panel/50'
      }`}
    >
      <Card.Content className="p-0">
        <div className="flex items-center justify-between mb-1">
          <div className="flex items-center gap-1">
            <Button
              isIconOnly
              variant="ghost"
              size="sm"
              className={`min-w-0 w-4 h-4 ${pinned ? 'text-yellow-500' : 'text-zhi-muted hover:text-zhi-text'}`}
              aria-label={pinned ? t('agents.unpin') : t('agents.pinToTop')}
              onPress={onTogglePin}
            >
              {pinned ? <PinFill className="size-2.5" /> : <Pin className="size-2.5" />}
            </Button>
            <span className="text-zhi-text font-medium">#{agent.id}</span>
            {agent.respawn_count > 0 && (
              <span className="text-zhi-muted">G{agent.respawn_count}</span>
            )}
          </div>
          <div className="flex items-center gap-1">
            <Button
              isIconOnly
              variant="ghost"
              size="sm"
              className={`min-w-0 w-4 h-4 ${tracked ? 'text-yellow-400' : 'text-zhi-muted hover:text-zhi-text'}`}
              aria-label={tracked ? t('agents.stopTracking') : t('agents.trackOnMap')}
              onPress={onTrack}
            >
              {tracked ? <DiamondFill className="size-2.5" /> : <Diamond className="size-2.5" />}
            </Button>
            <CircleFill className="size-2" style={{ color: hpColor }} />
          </div>
        </div>

        <div className="text-zhi-muted leading-relaxed">
          <div className="grid grid-cols-2 gap-x-2 gap-y-0">
            <StatPair label={t('agents.hp')} value={agent.existence.toFixed(1)} />
            <StatPair label={t('agents.stress')} value={agent.stress.toFixed(2)} warn={agent.stress > 1} />
            <StatPair label={t('agents.hunger')} value={agent.hunger.toFixed(1)} warn={warnHunger} warnColor="orange" />
            <StatPair label={t('agents.thirst')} value={agent.thirst.toFixed(1)} warn={warnThirst} warnColor="cyan" />
            <StatPair label={t('agents.stamina')} value={agent.stamina.toFixed(1)} warn={warnStamina} warnColor="yellow" />
            <StatPair label={t('agents.btemp')} value={`${agent.body_temperature.toFixed(1)}°C`} warn={warnCold || warnHot} warnColor={warnHot ? 'red' : 'blue'} />
            <StatPair label={t('agents.age')} value={`${agent.tick_count}t`} />
            <StatPair label={t('agents.action')} value={actionLabel(agent, terrain, gridW, t)} />
          </div>
          <div className="text-zhi-muted mt-0.5">
            <span>{t('agents.pos')} ({agent.x},{agent.y})</span>
          </div>
          <div className="flex gap-2 mt-0.5 text-zhi-muted">
            <span title={t('agents.food')}>F:{agent.food_eat_count}</span>
            <span title={t('agents.bigfood')}>B:{agent.bigfood_eat_count}</span>
            <span title={t('agents.corpse')}>C:{agent.corpse_eat_count}</span>
            <span title={t('agents.attacks')}>A:{agent.attack_count}</span>
            <span title={t('agents.chemicals')}>E:{agent.emit_count}</span>
          </div>
        </div>
      </Card.Content>
    </Card>
  );
}

function StatPair({ label, value, warn, warnColor }: {
  label: string; value: string; warn?: boolean; warnColor?: string;
}) {
  let valClass = 'text-zhi-text';
  if (warn) {
    switch (warnColor) {
      case 'cyan': valClass = 'text-cyan-400'; break;
      case 'yellow': valClass = 'text-yellow-400'; break;
      case 'blue': valClass = 'text-blue-400'; break;
      case 'red': valClass = 'text-red-400'; break;
      default: valClass = 'text-orange-400';
    }
  }
  return (
    <div className="flex justify-between gap-1">
      <span>{label}</span>
      <span className={valClass}>{value}</span>
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
