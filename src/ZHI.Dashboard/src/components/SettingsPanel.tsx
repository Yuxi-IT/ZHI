import { useState, useEffect } from 'react'

interface ZhiConfig {
  Grid: { Width: number; Height: number; InitialFood: number; InitialBigFood: number; FoodEnergy: number; BigFoodEnergy: number; FoodTTL: number; BigFoodTTL: number; MaxFood: number; FoodRespawnInterval: number; SmallFoodEatTicks: number; CorpseEatTicks: number; BigFoodSoloTicks: number; BigFoodCoopTicks: number; BigFoodMaxEaters: number; BigFoodSoloEnergyRatio: number }
  Cosmos: { AgentCount: number; RespawnDelayTicks: number; MutationStd: number }
  Temperature: { MaxTemp: number; MinTemp: number; ColdThreshold: number; MaxColdDecay: number; HotThreshold: number; MaxThirstAccel: number; HuddleRange: number; HuddleWarmthPerAgent: number }
  Combat: { AttackRange: number; StressPerAttack: number; StressDamage: number; StressDecay: number; AttackCost: number }
  Hunger: { DecayRate: number; EatRestore: number; PenaltyStart: number; MaxPenalty: number; Initial: number }
  Thirst: { DecayRate: number; DrinkRestore: number; PenaltyStart: number; MaxPenalty: number; Initial: number }
  River: { Width: number; DeepWidth: number; FordChance: number; SoundRange: number; SoundDecay: number }
  Existence: { DecayPerTick: number; Initial: number }
  Reproduce: { MinExistence: number; MinAge: number; Cooldown: number; ParentCost: number; ChildStart: number; MutationScale: number }
  AgeDeath: { MaxAge: number; Stage1Age: number; Stage1Decay: number; Stage2Age: number; Stage2Decay: number; Stage3Age: number; Stage3Decay: number }
}

const API = `${location.protocol}//${location.hostname}:8088`

function NumberField({ label, value, onChange, min, max, step = 1 }: { label: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number }) {
  return (
    <label className="flex items-center justify-between gap-2 py-0.5">
      <span className="text-[10px] text-neutral-400 shrink-0">{label}</span>
      <input
        type="number"
        value={value}
        onChange={e => onChange(parseFloat(e.target.value) || 0)}
        min={min} max={max} step={step}
        className="w-16 px-1 py-0 text-[10px] bg-neutral-800 border border-neutral-700 rounded text-neutral-300 text-right focus:outline-none focus:border-blue-600"
      />
    </label>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  const [open, setOpen] = useState(true)
  return (
    <div className="border-b border-neutral-800 pb-1">
      <button onClick={() => setOpen(o => !o)} className="flex items-center gap-1 w-full text-left py-1">
        <span className="text-[9px] text-neutral-500">{open ? '▾' : '▸'}</span>
        <span className="text-[10px] text-neutral-400 font-semibold">{title}</span>
      </button>
      {open && <div className="pl-3 pb-1 space-y-0">{children}</div>}
    </div>
  )
}

export function SettingsPanel() {
  const [config, setConfig] = useState<ZhiConfig | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => { fetchConfig() }, [])

  const fetchConfig = async () => {
    try {
      const r = await fetch(`${API}/api/config`)
      const data = await r.json()
      setConfig(data)
      setLoading(false)
    } catch {
      setStatus('加载配置失败')
      setLoading(false)
    }
  }

  const update = <K extends keyof ZhiConfig>(section: K, key: keyof ZhiConfig[K], value: number) => {
    if (!config) return
    setConfig({ ...config, [section]: { ...config[section], [key]: value } })
  }

  const save = async (restart: boolean) => {
    if (!config) return
    setStatus(restart ? '保存并重启中...' : '保存中...')
    try {
      const r = await fetch(`${API}/api/config${restart ? '?restart=true' : ''}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(config)
      })
      const data = await r.json()
      setStatus(data.ok ? (restart ? '已重启!' : '已保存') : '保存失败')
      if (restart) setStatus('世界已重启 — 刷新页面查看效果')
    } catch {
      setStatus('连接失败')
    }
  }

  if (loading) return <div className="text-[10px] text-neutral-500 p-4">加载配置...</div>
  if (!config) return <div className="text-[10px] text-red-400 p-4">{status}</div>

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <div className="flex-1 overflow-y-auto px-3 py-2 space-y-0.5">
        <Section title="世界">
          <NumberField label="宽度" value={config.Grid.Width} onChange={v => update('Grid', 'Width', v)} min={16} max={256} />
          <NumberField label="高度" value={config.Grid.Height} onChange={v => update('Grid', 'Height', v)} min={16} max={256} />
          <NumberField label="Agent 数量" value={config.Cosmos.AgentCount} onChange={v => update('Cosmos', 'AgentCount', v)} min={1} max={256} />
          <NumberField label="初始普通食物" value={config.Grid.InitialFood} onChange={v => update('Grid', 'InitialFood', v)} min={0} max={500} />
          <NumberField label="初始大食物" value={config.Grid.InitialBigFood} onChange={v => update('Grid', 'InitialBigFood', v)} min={0} max={100} />
          <NumberField label="食物上限" value={config.Grid.MaxFood} onChange={v => update('Grid', 'MaxFood', v)} min={1} max={1000} />
          <NumberField label="食物补充间隔(tick)" value={config.Grid.FoodRespawnInterval} onChange={v => update('Grid', 'FoodRespawnInterval', v)} min={0} max={1000} />
        </Section>

        <Section title="食物能量">
          <NumberField label="普通食物能量" value={config.Grid.FoodEnergy} onChange={v => update('Grid', 'FoodEnergy', v)} min={1} max={200} step={1} />
          <NumberField label="大食物能量" value={config.Grid.BigFoodEnergy} onChange={v => update('Grid', 'BigFoodEnergy', v)} min={1} max={500} step={1} />
          <NumberField label="普通食物TTL" value={config.Grid.FoodTTL} onChange={v => update('Grid', 'FoodTTL', v)} min={10} max={2000} />
          <NumberField label="大食物TTL" value={config.Grid.BigFoodTTL} onChange={v => update('Grid', 'BigFoodTTL', v)} min={10} max={5000} />
        </Section>

        <Section title="进食">
          <NumberField label="普通食物(tick)" value={config.Grid.SmallFoodEatTicks} onChange={v => update('Grid', 'SmallFoodEatTicks', v)} min={1} max={100} />
          <NumberField label="尸体(tick)" value={config.Grid.CorpseEatTicks} onChange={v => update('Grid', 'CorpseEatTicks', v)} min={1} max={100} />
          <NumberField label="大食物单人(tick)" value={config.Grid.BigFoodSoloTicks} onChange={v => update('Grid', 'BigFoodSoloTicks', v)} min={1} max={200} />
          <NumberField label="大食物合作(tick)" value={config.Grid.BigFoodCoopTicks} onChange={v => update('Grid', 'BigFoodCoopTicks', v)} min={1} max={100} />
          <NumberField label="大食物最大食者" value={config.Grid.BigFoodMaxEaters} onChange={v => update('Grid', 'BigFoodMaxEaters', v)} min={1} max={10} />
          <NumberField label="大食物单人能量比" value={config.Grid.BigFoodSoloEnergyRatio} onChange={v => update('Grid', 'BigFoodSoloEnergyRatio', v)} min={0.1} max={1} step={0.05} />
        </Section>

        <Section title="温度">
          <NumberField label="最高温度" value={config.Temperature.MaxTemp} onChange={v => update('Temperature', 'MaxTemp', v)} min={10} max={60} step={1} />
          <NumberField label="最低温度" value={config.Temperature.MinTemp} onChange={v => update('Temperature', 'MinTemp', v)} min={-20} max={30} step={1} />
          <NumberField label="低温阈值" value={config.Temperature.ColdThreshold} onChange={v => update('Temperature', 'ColdThreshold', v)} min={-10} max={40} step={1} />
          <NumberField label="最大寒冷衰减" value={config.Temperature.MaxColdDecay} onChange={v => update('Temperature', 'MaxColdDecay', v)} min={0} max={1} step={0.01} />
          <NumberField label="高温阈值" value={config.Temperature.HotThreshold} onChange={v => update('Temperature', 'HotThreshold', v)} min={10} max={50} step={1} />
          <NumberField label="抱团范围" value={config.Temperature.HuddleRange} onChange={v => update('Temperature', 'HuddleRange', v)} min={0} max={10} step={1} />
          <NumberField label="每邻居温暖度" value={config.Temperature.HuddleWarmthPerAgent} onChange={v => update('Temperature', 'HuddleWarmthPerAgent', v)} min={0} max={20} step={0.5} />
        </Section>

        <Section title="战斗">
          <NumberField label="攻击范围" value={config.Combat.AttackRange} onChange={v => update('Combat', 'AttackRange', v)} min={1} max={10} />
          <NumberField label="每次攻击压力" value={config.Combat.StressPerAttack} onChange={v => update('Combat', 'StressPerAttack', v)} min={0} max={5} step={0.1} />
          <NumberField label="压力伤害乘数" value={config.Combat.StressDamage} onChange={v => update('Combat', 'StressDamage', v)} min={0} max={1} step={0.01} />
          <NumberField label="攻击HP消耗" value={config.Combat.AttackCost} onChange={v => update('Combat', 'AttackCost', v)} min={0} max={50} step={0.5} />
        </Section>

        <Section title="河流">
          <NumberField label="宽度" value={config.River.Width} onChange={v => update('River', 'Width', v)} min={1} max={20} />
          <NumberField label="深水宽度" value={config.River.DeepWidth} onChange={v => update('River', 'DeepWidth', v)} min={0} max={10} />
          <NumberField label="浅滩概率%" value={config.River.FordChance} onChange={v => update('River', 'FordChance', v)} min={0} max={100} />
        </Section>

        <Section title="生理">
          <NumberField label="饥饿衰减" value={config.Hunger.DecayRate} onChange={v => update('Hunger', 'DecayRate', v)} min={0} max={1} step={0.01} />
          <NumberField label="进食恢复" value={config.Hunger.EatRestore} onChange={v => update('Hunger', 'EatRestore', v)} min={1} max={100} />
          <NumberField label="口渴衰减" value={config.Thirst.DecayRate} onChange={v => update('Thirst', 'DecayRate', v)} min={0} max={1} step={0.01} />
          <NumberField label="饮水恢复" value={config.Thirst.DrinkRestore} onChange={v => update('Thirst', 'DrinkRestore', v)} min={1} max={100} />
          <NumberField label="HP衰减" value={config.Existence.DecayPerTick} onChange={v => update('Existence', 'DecayPerTick', v)} min={0} max={2} step={0.01} />
        </Section>
      </div>

      <div className="shrink-0 px-3 py-2 border-t border-neutral-800 flex items-center gap-2">
        <button
          onClick={() => save(false)}
          className="px-2 py-1 text-[10px] bg-neutral-800 hover:bg-neutral-700 text-neutral-300 rounded border border-neutral-700"
        >
          保存
        </button>
        <button
          onClick={() => save(true)}
          className="px-2 py-1 text-[10px] bg-blue-800 hover:bg-blue-700 text-blue-200 rounded border border-blue-700"
        >
          保存并重启
        </button>
        {status && <span className="text-[10px] text-neutral-500 ml-1">{status}</span>}
      </div>
    </div>
  )
}
