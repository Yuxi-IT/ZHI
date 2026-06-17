import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, PieChart, Pie, Cell } from 'recharts';
import type { StatsData } from '../types';

interface Props {
  stats: StatsData | null;
  loading: boolean;
}

const CAUSE_COLORS: Record<string, string> = {
  SELF_TERMINATION: '#ef4444',
  CPU_OVERFLOW: '#3b82f6',
  MEMORY_OVERFLOW: '#06b6d4',
  EXISTENCE_DEPLETED: '#f59e0b',
  PROCESS_EXITED: '#6b7280',
};

export function StatsPanel({ stats, loading }: Props) {
  if (loading || !stats) {
    return (
      <div className="px-4 py-2 text-zhi-muted text-[10px]">Loading stats...</div>
    );
  }

  const pieData = Object.entries(stats.cause_distribution).map(([name, value]) => ({
    name: name.replace(/_/g, ' '),
    value,
    color: CAUSE_COLORS[name] ?? '#6b7280',
  }));

  const aliveData = stats.generations.map((g, i, arr) => {
    const window = arr.slice(Math.max(0, i - 9), i + 1);
    const avg = window.reduce((sum, x) => sum + x.alive_seconds, 0) / window.length;
    return { generation: g.generation, alive_seconds: g.alive_seconds, avg_10: parseFloat(avg.toFixed(2)) };
  });

  return (
    <div className="px-4 py-2 flex gap-4 items-start overflow-x-auto">
      {/* Stat cards */}
      <div className="flex gap-2 shrink-0">
        <MiniCard label="Gens" value={stats.total_generations} />
        <MiniCard label="Suicide All" value={`${((stats.suicide_rate_all ?? 0) * 100).toFixed(1)}%`} />
        <MiniCard label="Suicide R10" value={`${((stats.suicide_rate_recent_10 ?? 0) * 100).toFixed(1)}%`} />
        <MiniCard label="Avg Alive R10" value={`${(stats.avg_alive_seconds_recent_10 ?? 0).toFixed(1)}s`} />
        <MiniCard label="Deaths" value={stats.total_deaths} />
      </div>

      {/* Chart */}
      <div className="flex-1 min-w-[200px] h-20">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={aliveData} margin={{ top: 2, right: 5, bottom: 2, left: 0 }}>
            <XAxis dataKey="generation" stroke="#525252" tick={{ fontSize: 8 }} hide />
            <YAxis stroke="#525252" tick={{ fontSize: 8 }} width={30} />
            <Tooltip
              contentStyle={{ background: '#111', border: '1px solid #252525', borderRadius: '4px', fontSize: 10 }}
              labelStyle={{ color: '#d4d4d4' }}
            />
            <Line type="monotone" dataKey="alive_seconds" stroke="#525252" strokeWidth={0.5} dot={false} />
            <Line type="monotone" dataKey="avg_10" stroke="#a78bfa" strokeWidth={1.5} dot={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>

      {/* Pie */}
      <div className="h-20 w-20 shrink-0">
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie data={pieData} dataKey="value" nameKey="name" cx="50%" cy="50%" outerRadius={35} innerRadius={18} strokeWidth={0}>
              {pieData.map((entry, i) => (
                <Cell key={i} fill={entry.color} />
              ))}
            </Pie>
          </PieChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

function MiniCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="bg-zhi-panel border border-zhi-border rounded px-2 py-1 text-center">
      <div className="text-[8px] text-zhi-muted uppercase">{label}</div>
      <div className="text-[11px] text-zhi-text">{value}</div>
    </div>
  );
}
