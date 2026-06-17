import { useEffect, useRef } from 'react';
import type { LogMessage } from '../types';
import { useT } from '../i18n/I18nContext';

interface Props {
  logs: LogMessage[];
}

export function LogPanel({ logs }: Props) {
  const { t } = useT()
  const containerRef = useRef<HTMLDivElement>(null);
  const autoScrollRef = useRef(true);

  useEffect(() => {
    const el = containerRef.current;
    if (!el || !autoScrollRef.current) return;
    el.scrollTop = el.scrollHeight;
  }, [logs]);

  const handleScroll = () => {
    const el = containerRef.current;
    if (!el) return;
    autoScrollRef.current = el.scrollHeight - el.scrollTop - el.clientHeight < 40;
  };

  return (
    <div className="flex flex-col flex-1 min-h-0">
      <div className="px-4 py-1.5 text-[10px] text-zhi-muted uppercase tracking-wider shrink-0">
        {t('log.title')}
      </div>
      <div
        ref={containerRef}
        onScroll={handleScroll}
        className="flex-1 overflow-y-auto p-3 space-y-0.5"
      >
        {logs.map((msg, i) => (
          <LogLine key={i} msg={msg} />
        ))}
      </div>
    </div>
  );
}

function LogLine({ msg }: { msg: LogMessage }) {
  const text = msg.message || '';
  let color = 'text-neutral-400';
  if (text.includes('Gen')) color = 'text-yellow-400/70';
  else if (text.includes('Attack')) color = 'text-red-400/70';
  else if (text.includes('DEAD')) color = 'text-red-400';
  else if (text.includes('Reproduce')) color = 'text-purple-400';
  else if (text.includes('PPO')) color = 'text-blue-400/60';
  else if (text.includes('Cosmos')) color = 'text-cyan-400/60';
  else if (text.includes('initialized')) color = 'text-green-500/70';

  return (
    <div className="py-0.5 text-[11px] leading-relaxed">
      <span className="text-neutral-600">{msg.time} </span>
      <span className={color}>{text}</span>
    </div>
  );
}
