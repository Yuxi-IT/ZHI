import { useEffect, useRef, useState, useMemo, memo } from 'react';
import { Button } from '@heroui/react';
import { ArrowDownToLine, TrashBin } from '@gravity-ui/icons';
import type { WorldEvent, WorldEventType } from '../types';
import { useT } from '../i18n/I18nContext';

interface Props {
  events: WorldEvent[];
  energySource?: { food_pct: number; bigfood_pct: number; corpse_pct: number } | null;
  onClear?: () => void;
}

const EVENT_COLORS: Record<string, string> = {
  eat: 'text-green-400',
  attack: 'text-red-400',
  death: 'text-zhi-muted',
  reproduce: 'text-purple-400',
  signal: 'text-yellow-400',
  respawn: 'text-violet-400',
  push: 'text-amber-400',
  terraform: 'text-stone-400',
  flood: 'text-blue-400',
  weather: 'text-zhi-muted',
  dam_built: 'text-lime-400',
};

const FILTER_TYPES: WorldEventType[] = ['death', 'attack', 'respawn', 'eat', 'signal', 'push', 'terraform', 'flood', 'weather', 'dam_built'];

const FILTER_BORDER_COLORS: Record<string, string> = {
  death: 'border-zhi-muted',
  attack: 'border-red-600',
  respawn: 'border-violet-600',
  eat: 'border-green-600',
  signal: 'border-yellow-600',
  push: 'border-amber-600',
  terraform: 'border-stone-600',
  flood: 'border-blue-600',
  weather: 'border-zhi-muted',
  dam_built: 'border-lime-600',
};

const FILTER_TEXT_COLORS: Record<string, string> = {
  death: 'text-zhi-muted',
  attack: 'text-red-400',
  respawn: 'text-violet-400',
  eat: 'text-green-400',
  signal: 'text-yellow-400',
  push: 'text-amber-400',
  terraform: 'text-stone-400',
  flood: 'text-blue-400',
  weather: 'text-zhi-muted',
  dam_built: 'text-lime-400',
};

export const EventMonitor = memo(function EventMonitor({ events, energySource, onClear }: Props) {
  const { t } = useT();
  const scrollRef = useRef<HTMLDivElement>(null);
  const autoScrollRef = useRef(true);
  const [autoScroll, setAutoScroll] = useState(true);
  const [activeFilters, setActiveFilters] = useState<Set<WorldEventType>>(
    new Set(['death', 'attack', 'respawn', 'eat'])
  );

  const toggleFilter = (type: WorldEventType) => {
    setActiveFilters(prev => {
      const next = new Set(prev);
      if (next.has(type)) next.delete(type);
      else next.add(type);
      return next;
    });
  };

  const filteredEvents = useMemo(
    () => events.filter(e => activeFilters.has(e.type)),
    [events, activeFilters]
  );

  useEffect(() => {
    const el = scrollRef.current;
    if (!el) return;
    if (autoScroll) {
      el.scrollTop = el.scrollHeight;
    }
  }, [filteredEvents, autoScroll]);

  const handleScroll = () => {
    const el = scrollRef.current;
    if (!el) return;
    const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
    autoScrollRef.current = atBottom;
    if (atBottom !== autoScroll) setAutoScroll(atBottom);
  };

  const formatEvent = (e: WorldEvent): string => {
    switch (e.type) {
      case 'eat':
        return t('events.ate', { id: e.agent_id, type: e.food_type ?? '?', val: e.value.toFixed(1) });
      case 'attack':
        return t('events.attacked', { attacker: e.agent_id, target: e.target_id ?? '?', val: e.value.toFixed(1) });
      case 'death':
        return t('events.dead', { id: e.agent_id });
      case 'reproduce':
        return t('events.reproduced', { parent: e.agent_id, child: e.child_id ?? '?' });
      case 'signal':
        return t('events.signaled', { id: e.agent_id, val: e.signal_value ?? 0 });
      case 'respawn':
        return t('events.respawned', { id: e.agent_id });
      case 'push':
        return t('events.pushed', { id: e.agent_id, type: e.food_type ?? '?' });
      case 'terraform':
        return t('events.terraformed', { id: e.agent_id });
      case 'flood':
        return t('events.flooded');
      case 'weather':
        return t('events.weathered', { type: e.food_type ?? '?' });
      case 'dam_built':
        return t('events.damBuilt');
      default:
        return JSON.stringify(e);
    }
  };

  return (
    <div className="h-full flex flex-col min-h-0">
      <div className="px-3 py-1.5 border-b border-zhi-border shrink-0">
        <div className="text-zhi-muted text-[10px] flex items-center justify-between">
          <span>{t('events.title')} ({filteredEvents.length})</span>
          <div className="flex items-center gap-0.5">
            <Button
              isIconOnly
              variant="ghost"
              size="sm"
              className={`min-w-0 w-5 h-5 ${autoScroll ? 'text-cyan-400' : 'text-zhi-muted hover:text-zhi-text'}`}
              aria-label={autoScroll ? t('events.autoScrollOn') : t('events.autoScrollOff')}
              onPress={() => {
                setAutoScroll(v => !v);
                autoScrollRef.current = !autoScrollRef.current;
                if (!autoScroll) {
                  const el = scrollRef.current;
                  if (el) el.scrollTop = el.scrollHeight;
                }
              }}
            >
              <ArrowDownToLine className="size-3" />
            </Button>
            {onClear && (
              <Button
                isIconOnly
                variant="ghost"
                size="sm"
                className="min-w-0 w-5 h-5 text-zhi-muted hover:text-red-400"
                aria-label={t('events.clearAll')}
                onPress={onClear}
              >
                <TrashBin className="size-3" />
              </Button>
            )}
          </div>
        </div>
        {energySource && (
          <div className="text-[9px] text-zhi-muted mt-0.5 flex gap-2">
            <span>{t('events.energyF', { pct: energySource.food_pct.toFixed(0) })}</span>
            <span>{t('events.energyB', { pct: energySource.bigfood_pct.toFixed(0) })}</span>
            <span>{t('events.energyC', { pct: energySource.corpse_pct.toFixed(0) })}</span>
          </div>
        )}
      </div>

      <div className="px-2 py-1 border-b border-zhi-border shrink-0 flex flex-wrap gap-1">
        {FILTER_TYPES.map(type => (
          <Button
            key={type}
            variant={activeFilters.has(type) ? 'secondary' : 'ghost'}
            size="sm"
            className={`text-[9px] min-w-0 h-auto px-1.5 py-0.5 rounded border ${
              activeFilters.has(type)
                ? `${FILTER_BORDER_COLORS[type]} ${FILTER_TEXT_COLORS[type]} bg-zhi-border/50`
                : 'border-zhi-border text-zhi-muted'
            }`}
            onPress={() => toggleFilter(type)}
          >
            {t(`events.${type}`)}
          </Button>
        ))}
      </div>

      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto px-2 py-1 space-y-0.5 text-[10px] font-mono"
      >
        {filteredEvents.map((e, i) => (
          <div key={i} className={EVENT_COLORS[e.type] ?? 'text-zhi-text'}>
            <span className="text-zhi-muted mr-1">{e.tick}</span>
            {formatEvent(e)}
          </div>
        ))}
      </div>
    </div>
  );
});
