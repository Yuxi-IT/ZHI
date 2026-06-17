import { LineChart, Line, XAxis, YAxis, Tooltip, ResponsiveContainer, AreaChart, Area } from 'recharts'
import type { EcoDataPoint } from '../hooks/useEcoHistory'
import { useT } from '../i18n/I18nContext'

interface Props {
  data: EcoDataPoint[]
}

export function ChartsPanel({ data }: Props) {
  const { t } = useT()

  if (data.length < 2) {
    return (
      <div className="h-full flex items-center justify-center text-neutral-600 text-[10px]">
        {t('charts.collecting')}
      </div>
    )
  }

  return (
    <div className="h-full overflow-y-auto p-2 space-y-2">
      <ChartCard title={t('charts.population')}>
        <ResponsiveContainer width="100%" height={80}>
          <LineChart data={data}>
            <XAxis dataKey="t" hide />
            <YAxis hide domain={[0, 'auto']} />
            <Tooltip content={<MiniTooltip />} />
            <Line type="monotone" dataKey="alive" stroke="#22c55e" dot={false} strokeWidth={1.5} name="Alive" />
            <Line type="monotone" dataKey="food" stroke="#facc15" dot={false} strokeWidth={1} name="Food" />
            <Line type="monotone" dataKey="corpses" stroke="#94a3b8" dot={false} strokeWidth={1} name="Corpses" />
          </LineChart>
        </ResponsiveContainer>
      </ChartCard>

      <ChartCard title={t('charts.birthsDeaths')}>
        <ResponsiveContainer width="100%" height={60}>
          <AreaChart data={data}>
            <XAxis dataKey="t" hide />
            <YAxis hide domain={[0, 'auto']} />
            <Tooltip content={<MiniTooltip />} />
            <Area type="monotone" dataKey="births" stroke="#a78bfa" fill="#a78bfa33" strokeWidth={1} name="Births" />
            <Area type="monotone" dataKey="deaths" stroke="#ef4444" fill="#ef444433" strokeWidth={1} name="Deaths" />
          </AreaChart>
        </ResponsiveContainer>
      </ChartCard>

      <ChartCard title={t('charts.totalEnergy')}>
        <ResponsiveContainer width="100%" height={60}>
          <LineChart data={data}>
            <XAxis dataKey="t" hide />
            <YAxis hide domain={[0, 'auto']} />
            <Tooltip content={<MiniTooltip />} />
            <Line type="monotone" dataKey="energy" stroke="#38bdf8" dot={false} strokeWidth={1.5} name="Energy" />
          </LineChart>
        </ResponsiveContainer>
      </ChartCard>
    </div>
  )
}

function ChartCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded border border-neutral-800 bg-neutral-900/50 p-2">
      <div className="text-[9px] text-neutral-500 mb-1 uppercase tracking-wider">{title}</div>
      {children}
    </div>
  )
}

function MiniTooltip({ active, payload, label }: { active?: boolean; payload?: Array<{ value: number; name: string; color: string }>; label?: number }) {
  if (!active || !payload?.length) return null
  return (
    <div className="bg-neutral-900/95 border border-neutral-700 rounded px-2 py-1 text-[9px]">
      <div className="text-neutral-500 mb-0.5">t={label}</div>
      {payload.map((p, i) => (
        <div key={i} className="flex items-center gap-1.5">
          <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: p.color }} />
          <span className="text-neutral-400">{p.name}</span>
          <span className="text-neutral-200">{typeof p.value === 'number' ? p.value.toFixed(0) : p.value}</span>
        </div>
      ))}
    </div>
  )
}
