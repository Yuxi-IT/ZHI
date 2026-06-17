import { useState, useEffect } from 'react'
import { useT } from '../i18n/I18nContext'

interface ZhiConfig {
  Grid: { Width: number; Height: number; InitialFood: number; InitialBigFood: number; FoodEnergy: number; BigFoodEnergy: number; FoodDecayPerTick: number; BigFoodDecayPerTick: number; MaxFood: number; FoodRespawnInterval: number; FoodPerTickEnergy: number; BigFoodPerTickEnergy: number; CorpsePerTickEnergy: number }
  Cosmos: { AgentCount: number; RespawnDelayTicks: number; MutationStd: number }
  Temperature: { MaxTemp: number; MinTemp: number; ColdThreshold: number; MaxColdDecay: number; HotThreshold: number; MaxThirstAccel: number; HuddleRange: number; HuddleWarmthPerAgent: number; AgentBodyHeat: number; RiverCooling: number; RiverCoolingRange: number }
  Combat: { AttackRange: number; StressPerAttack: number; StressDamage: number; StressDecay: number; AttackCost: number }
  Hunger: { DecayRate: number; PenaltyStart: number; MaxPenalty: number; Initial: number }
  Thirst: { DecayRate: number; DrinkRestore: number; PenaltyStart: number; MaxPenalty: number; Initial: number }
  River: { Count: number; Width: number; DeepWidth: number; FordChance: number; SoundRange: number; SoundDecay: number }
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
      {open && <div className="pl-2 pb-1 grid grid-cols-2 gap-x-3 gap-y-0">{children}</div>}
    </div>
  )
}

export function SettingsPanel() {
  const { t } = useT()
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
      setStatus(t('settings.loadFailed'))
      setLoading(false)
    }
  }

  const update = <K extends keyof ZhiConfig>(section: K, key: keyof ZhiConfig[K], value: number) => {
    if (!config) return
    setConfig({ ...config, [section]: { ...config[section], [key]: value } })
  }

  const save = async (restart: boolean) => {
    if (!config) return
    setStatus(restart ? t('settings.saveRestart') : t('settings.saving'))
    try {
      const r = await fetch(`${API}/api/config${restart ? '?restart=true' : ''}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(config)
      })
      const data = await r.json()
      setStatus(data.ok ? (restart ? t('settings.restarted') : t('settings.saved')) : t('settings.saveFailed'))
    } catch {
      setStatus(t('settings.connectFailed'))
    }
  }

  if (loading) return <div className="text-[10px] text-neutral-500 p-4">{t('settings.loading')}</div>
  if (!config) return <div className="text-[10px] text-red-400 p-4">{status}</div>

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <div className="flex-1 overflow-y-auto px-3 py-2 space-y-0.5">
        <Section title={t('settings.world')}>
          <NumberField label={t('settings.width')} value={config.Grid.Width} onChange={v => update('Grid', 'Width', v)} min={16} max={256} />
          <NumberField label={t('settings.height')} value={config.Grid.Height} onChange={v => update('Grid', 'Height', v)} min={16} max={256} />
          <NumberField label={t('settings.agentCount')} value={config.Cosmos.AgentCount} onChange={v => update('Cosmos', 'AgentCount', v)} min={1} max={256} />
          <NumberField label={t('settings.initialFood')} value={config.Grid.InitialFood} onChange={v => update('Grid', 'InitialFood', v)} min={0} max={500} />
          <NumberField label={t('settings.initialBigFood')} value={config.Grid.InitialBigFood} onChange={v => update('Grid', 'InitialBigFood', v)} min={0} max={100} />
          <NumberField label={t('settings.maxFood')} value={config.Grid.MaxFood} onChange={v => update('Grid', 'MaxFood', v)} min={1} max={1000} />
          <NumberField label={t('settings.foodRespawnInterval')} value={config.Grid.FoodRespawnInterval} onChange={v => update('Grid', 'FoodRespawnInterval', v)} min={0} max={1000} />
        </Section>

        <Section title={t('settings.foodEnergySection')}>
          <NumberField label={t('settings.foodEnergy')} value={config.Grid.FoodEnergy} onChange={v => update('Grid', 'FoodEnergy', v)} min={1} max={200} step={1} />
          <NumberField label={t('settings.bigFoodEnergy')} value={config.Grid.BigFoodEnergy} onChange={v => update('Grid', 'BigFoodEnergy', v)} min={1} max={500} step={1} />
          <NumberField label={t('settings.foodDecay')} value={config.Grid.FoodDecayPerTick} onChange={v => update('Grid', 'FoodDecayPerTick', v)} min={0.001} max={1} step={0.005} />
          <NumberField label={t('settings.bigFoodDecay')} value={config.Grid.BigFoodDecayPerTick} onChange={v => update('Grid', 'BigFoodDecayPerTick', v)} min={0.001} max={1} step={0.005} />
        </Section>

        <Section title={t('settings.eating')}>
          <NumberField label={t('settings.foodPerTick')} value={config.Grid.FoodPerTickEnergy} onChange={v => update('Grid', 'FoodPerTickEnergy', v)} min={0.1} max={20} step={0.1} />
          <NumberField label={t('settings.bigFoodPerTick')} value={config.Grid.BigFoodPerTickEnergy} onChange={v => update('Grid', 'BigFoodPerTickEnergy', v)} min={0.1} max={50} step={0.1} />
          <NumberField label={t('settings.corpsePerTick')} value={config.Grid.CorpsePerTickEnergy} onChange={v => update('Grid', 'CorpsePerTickEnergy', v)} min={0.1} max={20} step={0.1} />
        </Section>

        <Section title={t('settings.temperature')}>
          <NumberField label={t('settings.maxTemp')} value={config.Temperature.MaxTemp} onChange={v => update('Temperature', 'MaxTemp', v)} min={10} max={60} step={1} />
          <NumberField label={t('settings.minTemp')} value={config.Temperature.MinTemp} onChange={v => update('Temperature', 'MinTemp', v)} min={-20} max={30} step={1} />
          <NumberField label={t('settings.coldThreshold')} value={config.Temperature.ColdThreshold} onChange={v => update('Temperature', 'ColdThreshold', v)} min={-10} max={40} step={1} />
          <NumberField label={t('settings.maxColdDecay')} value={config.Temperature.MaxColdDecay} onChange={v => update('Temperature', 'MaxColdDecay', v)} min={0} max={1} step={0.01} />
          <NumberField label={t('settings.hotThreshold')} value={config.Temperature.HotThreshold} onChange={v => update('Temperature', 'HotThreshold', v)} min={10} max={50} step={1} />
          <NumberField label={t('settings.huddleRange')} value={config.Temperature.HuddleRange} onChange={v => update('Temperature', 'HuddleRange', v)} min={0} max={10} step={1} />
          <NumberField label={t('settings.huddleWarmth')} value={config.Temperature.HuddleWarmthPerAgent} onChange={v => update('Temperature', 'HuddleWarmthPerAgent', v)} min={0} max={20} step={0.5} />
          <NumberField label={t('settings.agentBodyHeat')} value={config.Temperature.AgentBodyHeat} onChange={v => update('Temperature', 'AgentBodyHeat', v)} min={0} max={10} step={0.5} />
          <NumberField label={t('settings.riverCooling')} value={config.Temperature.RiverCooling} onChange={v => update('Temperature', 'RiverCooling', v)} min={0} max={20} step={0.5} />
          <NumberField label={t('settings.coolingRange')} value={config.Temperature.RiverCoolingRange} onChange={v => update('Temperature', 'RiverCoolingRange', v)} min={0} max={5} />
        </Section>

        <Section title={t('settings.combat')}>
          <NumberField label={t('settings.attackRange')} value={config.Combat.AttackRange} onChange={v => update('Combat', 'AttackRange', v)} min={1} max={10} />
          <NumberField label={t('settings.stressPerAttack')} value={config.Combat.StressPerAttack} onChange={v => update('Combat', 'StressPerAttack', v)} min={0} max={5} step={0.1} />
          <NumberField label={t('settings.stressDamage')} value={config.Combat.StressDamage} onChange={v => update('Combat', 'StressDamage', v)} min={0} max={1} step={0.01} />
          <NumberField label={t('settings.attackCost')} value={config.Combat.AttackCost} onChange={v => update('Combat', 'AttackCost', v)} min={0} max={50} step={0.5} />
        </Section>

        <Section title={t('settings.river')}>
          <NumberField label={t('settings.riverCount')} value={config.River.Count} onChange={v => update('River', 'Count', v)} min={0} max={10} />
          <NumberField label={t('settings.riverWidth')} value={config.River.Width} onChange={v => update('River', 'Width', v)} min={1} max={20} />
          <NumberField label={t('settings.riverDeepWidth')} value={config.River.DeepWidth} onChange={v => update('River', 'DeepWidth', v)} min={0} max={10} />
          <NumberField label={t('settings.fordChance')} value={config.River.FordChance} onChange={v => update('River', 'FordChance', v)} min={0} max={100} />
        </Section>

        <Section title={t('settings.physiology')}>
          <NumberField label={t('settings.hungerDecay')} value={config.Hunger.DecayRate} onChange={v => update('Hunger', 'DecayRate', v)} min={0} max={1} step={0.01} />
          <NumberField label={t('settings.thirstDecay')} value={config.Thirst.DecayRate} onChange={v => update('Thirst', 'DecayRate', v)} min={0} max={1} step={0.01} />
          <NumberField label={t('settings.drinkRestore')} value={config.Thirst.DrinkRestore} onChange={v => update('Thirst', 'DrinkRestore', v)} min={1} max={100} />
          <NumberField label={t('settings.hpDecay')} value={config.Existence.DecayPerTick} onChange={v => update('Existence', 'DecayPerTick', v)} min={0} max={2} step={0.01} />
        </Section>
      </div>

      <div className="shrink-0 px-3 py-2 border-t border-neutral-800 flex items-center gap-2">
        <button
          onClick={() => save(false)}
          className="px-2 py-1 text-[10px] bg-neutral-800 hover:bg-neutral-700 text-neutral-300 rounded border border-neutral-700"
        >
          {t('settings.save')}
        </button>
        <button
          onClick={() => save(true)}
          className="px-2 py-1 text-[10px] bg-blue-800 hover:bg-blue-700 text-blue-200 rounded border border-blue-700"
        >
          {t('settings.saveAndRestart')}
        </button>
        {status && <span className="text-[10px] text-neutral-500 ml-1">{status}</span>}
      </div>
    </div>
  )
}
