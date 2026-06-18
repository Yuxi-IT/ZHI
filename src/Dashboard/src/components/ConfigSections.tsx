import { TextField, Label, Input, Disclosure } from '@heroui/react';
import { ChevronDown } from '@gravity-ui/icons';
import { useT } from '../i18n/I18nContext';

export interface ZhiConfig {
  Grid: { Width: number; Height: number; InitialFood: number; InitialBigFood: number; FoodEnergy: number; BigFoodEnergy: number; FoodDecayPerTick: number; BigFoodDecayPerTick: number; MaxFood: number; FoodRespawnInterval: number; FoodPerTickEnergy: number; BigFoodPerTickEnergy: number; CorpsePerTickEnergy: number };
  Cosmos: { AgentCount: number; RespawnDelayTicks: number; MutationStd: number };
  Temperature: { MaxTemp: number; MinTemp: number; ColdThreshold: number; MaxColdDecay: number; HotThreshold: number; MaxThirstAccel: number; HuddleRange: number; HuddleWarmthPerAgent: number; AgentBodyHeat: number; RiverCooling: number; RiverCoolingRange: number };
  Combat: { AttackRange: number; StressPerAttack: number; StressDamage: number; AttackCost: number };
  Hunger: { DecayRate: number; PenaltyStart: number; MaxPenalty: number; Initial: number };
  Thirst: { DecayRate: number; DrinkRestore: number; PenaltyStart: number; MaxPenalty: number; Initial: number };
  River: { Count: number; Width: number; DeepWidth: number; FordChance: number; SoundRange: number; SoundDecay: number };
  Existence: { DecayPerTick: number; Initial: number };
  Reproduce: { MinExistence: number; MinAge: number; Cooldown: number; ParentCost: number; ChildStart: number; MutationScale: number };
  AgeDeath: { MaxAge: number; Stage1Age: number; Stage1Decay: number; Stage2Age: number; Stage2Decay: number; Stage3Age: number; Stage3Decay: number };
}

export const DEFAULT_CONFIG: ZhiConfig = {
  Grid: { Width: 64, Height: 64, InitialFood: 120, InitialBigFood: 20, FoodEnergy: 30, BigFoodEnergy: 100, FoodDecayPerTick: 0.002, BigFoodDecayPerTick: 0.001, MaxFood: 300, FoodRespawnInterval: 60, FoodPerTickEnergy: 3, BigFoodPerTickEnergy: 8, CorpsePerTickEnergy: 2 },
  Cosmos: { AgentCount: 30, RespawnDelayTicks: 30, MutationStd: 0.1 },
  Temperature: { MaxTemp: 40, MinTemp: -5, ColdThreshold: 10, MaxColdDecay: 0.5, HotThreshold: 30, MaxThirstAccel: 0.3, HuddleRange: 2, HuddleWarmthPerAgent: 3, AgentBodyHeat: 2, RiverCooling: 5, RiverCoolingRange: 2 },
  Combat: { AttackRange: 3, StressPerAttack: 1.5, StressDamage: 0.15, AttackCost: 5 },
  Hunger: { DecayRate: 0.04, PenaltyStart: 30, MaxPenalty: 0.03, Initial: 100 },
  Thirst: { DecayRate: 0.025, DrinkRestore: 40, PenaltyStart: 20, MaxPenalty: 0.04, Initial: 100 },
  River: { Count: 2, Width: 3, DeepWidth: 1, FordChance: 30, SoundRange: 15, SoundDecay: 0.05 },
  Existence: { DecayPerTick: 0.1, Initial: 100 },
  Reproduce: { MinExistence: 60, MinAge: 100, Cooldown: 50, ParentCost: 30, ChildStart: 50, MutationScale: 0.03 },
  AgeDeath: { MaxAge: 800, Stage1Age: 400, Stage1Decay: 0.05, Stage2Age: 600, Stage2Decay: 0.15, Stage3Age: 700, Stage3Decay: 0.3 },
};

export function NumberField({ label, value, onChange, min, max, step }: {
  label: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number;
}) {
  return (
    <TextField className="w-full">
      <div className="flex items-center justify-between gap-2 py-0.5">
        <Label className="text-[10px] text-zhi-muted shrink-0">{label}</Label>
        <Input
          type="number"
          value={value}
          onChange={e => onChange(parseFloat(e.target.value) || 0)}
          min={min} max={max} step={step ?? 1}
          className="w-20 text-[10px] text-right"
        />
      </div>
    </TextField>
  );
}

export function ConfigSection({ title, defaultExpanded, children }: { title: string; defaultExpanded?: boolean; children: React.ReactNode }) {
  return (
    <Disclosure defaultExpanded={defaultExpanded}>
      <Disclosure.Trigger className="flex items-center gap-1 w-full text-left py-1 group">
        <ChevronDown className="size-3 text-zhi-muted transition-transform group-data-[expanded]:rotate-180" />
        <span className="text-[10px] text-zhi-text font-semibold">{title}</span>
      </Disclosure.Trigger>
      <Disclosure.Content className="pl-2 pb-1">
        <div className="space-y-0">{children}</div>
      </Disclosure.Content>
    </Disclosure>
  );
}

export function ConfigFormFields({ config, update }: {
  config: ZhiConfig;
  update: <K extends keyof ZhiConfig>(section: K, key: keyof ZhiConfig[K], value: number) => void;
}) {
  const { t } = useT();
  return (
    <div className="space-y-0.5">
      <ConfigSection title={t('settings.world')} defaultExpanded>
        <NumberField label={t('settings.width')} value={config.Grid.Width} onChange={v => update('Grid', 'Width', v)} min={16} max={256} />
        <NumberField label={t('settings.height')} value={config.Grid.Height} onChange={v => update('Grid', 'Height', v)} min={16} max={256} />
        <NumberField label={t('settings.agentCount')} value={config.Cosmos.AgentCount} onChange={v => update('Cosmos', 'AgentCount', v)} min={1} max={256} />
        <NumberField label={t('settings.initialFood')} value={config.Grid.InitialFood} onChange={v => update('Grid', 'InitialFood', v)} min={0} max={500} />
        <NumberField label={t('settings.initialBigFood')} value={config.Grid.InitialBigFood} onChange={v => update('Grid', 'InitialBigFood', v)} min={0} max={100} />
        <NumberField label={t('settings.maxFood')} value={config.Grid.MaxFood} onChange={v => update('Grid', 'MaxFood', v)} min={1} max={1000} />
        <NumberField label={t('settings.foodRespawnInterval')} value={config.Grid.FoodRespawnInterval} onChange={v => update('Grid', 'FoodRespawnInterval', v)} min={0} max={1000} />
      </ConfigSection>

      <ConfigSection title={t('settings.foodEnergySection')}>
        <NumberField label={t('settings.foodEnergy')} value={config.Grid.FoodEnergy} onChange={v => update('Grid', 'FoodEnergy', v)} min={1} max={200} />
        <NumberField label={t('settings.bigFoodEnergy')} value={config.Grid.BigFoodEnergy} onChange={v => update('Grid', 'BigFoodEnergy', v)} min={1} max={500} />
        <NumberField label={t('settings.foodDecay')} value={config.Grid.FoodDecayPerTick} onChange={v => update('Grid', 'FoodDecayPerTick', v)} min={0.001} max={1} step={0.005} />
        <NumberField label={t('settings.bigFoodDecay')} value={config.Grid.BigFoodDecayPerTick} onChange={v => update('Grid', 'BigFoodDecayPerTick', v)} min={0.001} max={1} step={0.005} />
      </ConfigSection>

      <ConfigSection title={t('settings.eating')}>
        <NumberField label={t('settings.foodPerTick')} value={config.Grid.FoodPerTickEnergy} onChange={v => update('Grid', 'FoodPerTickEnergy', v)} min={0.1} max={20} step={0.1} />
        <NumberField label={t('settings.bigFoodPerTick')} value={config.Grid.BigFoodPerTickEnergy} onChange={v => update('Grid', 'BigFoodPerTickEnergy', v)} min={0.1} max={50} step={0.1} />
        <NumberField label={t('settings.corpsePerTick')} value={config.Grid.CorpsePerTickEnergy} onChange={v => update('Grid', 'CorpsePerTickEnergy', v)} min={0.1} max={20} step={0.1} />
      </ConfigSection>

      <ConfigSection title={t('settings.temperature')}>
        <NumberField label={t('settings.maxTemp')} value={config.Temperature.MaxTemp} onChange={v => update('Temperature', 'MaxTemp', v)} min={10} max={60} />
        <NumberField label={t('settings.minTemp')} value={config.Temperature.MinTemp} onChange={v => update('Temperature', 'MinTemp', v)} min={-20} max={30} />
        <NumberField label={t('settings.coldThreshold')} value={config.Temperature.ColdThreshold} onChange={v => update('Temperature', 'ColdThreshold', v)} min={-10} max={40} />
        <NumberField label={t('settings.maxColdDecay')} value={config.Temperature.MaxColdDecay} onChange={v => update('Temperature', 'MaxColdDecay', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.hotThreshold')} value={config.Temperature.HotThreshold} onChange={v => update('Temperature', 'HotThreshold', v)} min={10} max={50} />
        <NumberField label={t('settings.huddleRange')} value={config.Temperature.HuddleRange} onChange={v => update('Temperature', 'HuddleRange', v)} min={0} max={10} />
        <NumberField label={t('settings.huddleWarmth')} value={config.Temperature.HuddleWarmthPerAgent} onChange={v => update('Temperature', 'HuddleWarmthPerAgent', v)} min={0} max={20} step={0.5} />
        <NumberField label={t('settings.agentBodyHeat')} value={config.Temperature.AgentBodyHeat} onChange={v => update('Temperature', 'AgentBodyHeat', v)} min={0} max={10} step={0.5} />
        <NumberField label={t('settings.riverCooling')} value={config.Temperature.RiverCooling} onChange={v => update('Temperature', 'RiverCooling', v)} min={0} max={20} step={0.5} />
        <NumberField label={t('settings.coolingRange')} value={config.Temperature.RiverCoolingRange} onChange={v => update('Temperature', 'RiverCoolingRange', v)} min={0} max={5} />
      </ConfigSection>

      <ConfigSection title={t('settings.combat')}>
        <NumberField label={t('settings.attackRange')} value={config.Combat.AttackRange} onChange={v => update('Combat', 'AttackRange', v)} min={1} max={10} />
        <NumberField label={t('settings.stressPerAttack')} value={config.Combat.StressPerAttack} onChange={v => update('Combat', 'StressPerAttack', v)} min={0} max={5} step={0.1} />
        <NumberField label={t('settings.stressDamage')} value={config.Combat.StressDamage} onChange={v => update('Combat', 'StressDamage', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.attackCost')} value={config.Combat.AttackCost} onChange={v => update('Combat', 'AttackCost', v)} min={0} max={50} step={0.5} />
      </ConfigSection>

      <ConfigSection title={t('settings.river')}>
        <NumberField label={t('settings.riverCount')} value={config.River.Count} onChange={v => update('River', 'Count', v)} min={0} max={10} />
        <NumberField label={t('settings.riverWidth')} value={config.River.Width} onChange={v => update('River', 'Width', v)} min={1} max={20} />
        <NumberField label={t('settings.riverDeepWidth')} value={config.River.DeepWidth} onChange={v => update('River', 'DeepWidth', v)} min={0} max={10} />
        <NumberField label={t('settings.fordChance')} value={config.River.FordChance} onChange={v => update('River', 'FordChance', v)} min={0} max={100} />
      </ConfigSection>

      <ConfigSection title={t('settings.physiology')}>
        <NumberField label={t('settings.hungerDecay')} value={config.Hunger.DecayRate} onChange={v => update('Hunger', 'DecayRate', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.thirstDecay')} value={config.Thirst.DecayRate} onChange={v => update('Thirst', 'DecayRate', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.drinkRestore')} value={config.Thirst.DrinkRestore} onChange={v => update('Thirst', 'DrinkRestore', v)} min={1} max={100} />
        <NumberField label={t('settings.hpDecay')} value={config.Existence.DecayPerTick} onChange={v => update('Existence', 'DecayPerTick', v)} min={0} max={2} step={0.01} />
      </ConfigSection>
    </div>
  );
}
