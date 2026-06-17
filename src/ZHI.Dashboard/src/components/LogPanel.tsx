import { useEffect, useRef } from 'react';
import type { LogMessage } from '../types';

interface Props {
  logs: LogMessage[];
}

export function LogPanel({ logs }: Props) {
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
    <div className="border-t border-zhi-border flex flex-col flex-1 min-h-0">
      <div className="px-4 py-1.5 text-[10px] text-zhi-muted uppercase tracking-wider border-b border-zhi-border shrink-0">
        log
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
  if (text.includes('遗传')) color = 'text-yellow-400/70';
  else if (text.includes('Steal')) color = 'text-orange-400';
  else if (text.includes('Attack')) color = 'text-red-400/70';
  else if (text.includes('死了')) color = 'text-red-400';
  else if (text.includes('TERMINATION') || text.includes('OVERFLOW') || text.includes('DEPLETED')) color = 'text-red-500/70';
  else if (text.includes('存活:')) color = 'text-neutral-500';
  else if (text.includes('初始化')) color = 'text-green-500/70';
  else if (text.includes('启动中')) color = 'text-green-500/70';
  else if (text.includes('已连接')) color = 'text-green-400/50';
  else if (text.includes('think成功')) color = 'text-purple-400';
  else if (text.includes('expand')) color = 'text-zhi-accent';
  else if (text.includes('Cosmos')) color = 'text-cyan-400/60';

  return (
    <div className="py-0.5 text-[11px] leading-relaxed">
      <span className="text-neutral-600">{msg.time} </span>
      <span className={color}>{text}</span>
    </div>
  );
}
