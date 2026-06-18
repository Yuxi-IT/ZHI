import { TextField, Label, Input, Disclosure } from '@heroui/react';
import { ChevronDown } from '@gravity-ui/icons';
import { useT } from '../i18n/I18nContext';

export interface ZhiConfig {
  grid: { width: number; height: number; initial_food: number; initial_big_food: number; food_energy: number; big_food_energy: number; food_decay_per_tick: number; big_food_decay_per_tick: number; max_food: number; food_respawn_interval: number; food_per_tick_energy: number; big_food_per_tick_energy: number; corpse_per_tick_energy: number };
  cosmos: { agent_count: number; respawn_delay_ticks: number; mutation_std: number };
  temperature: { max_temp: number; min_temp: number; cold_threshold: number; max_cold_decay: number; hot_threshold: number; max_thirst_accel: number; huddle_range: number; huddle_warmth_per_agent: number; agent_body_heat: number; land_lerp_rate: number; water_heat_capacity: number; thermal_diffusion_rate: number; river_land_influence: number };
  combat: { attack_range: number; stress_per_attack: number; stress_damage: number; attack_cost: number };
  hunger: { decay_rate: number; penalty_start: number; max_penalty: number; initial: number };
  thirst: { decay_rate: number; drink_restore: number; penalty_start: number; max_penalty: number; initial: number };
  river: { count: number; width: number; deep_width: number; ford_chance: number; sound_range: number; sound_decay: number };
  existence: { decay_per_tick: number; initial: number };
  reproduce: { min_existence: number; min_age: number; cooldown: number; parent_cost: number; child_start: number; mutation_scale: number };
  age_death: { max_age: number; stage1_age: number; stage1_decay: number; stage2_age: number; stage2_decay: number; stage3_age: number; stage3_decay: number };
}

export const DEFAULT_CONFIG: ZhiConfig = {
  grid: { width: 64, height: 64, initial_food: 70, initial_big_food: 10, food_energy: 30, big_food_energy: 100, food_decay_per_tick: 0.002, big_food_decay_per_tick: 0.001, max_food: 100, food_respawn_interval: 60, food_per_tick_energy: 3, big_food_per_tick_energy: 8, corpse_per_tick_energy: 2 },
  cosmos: { agent_count: 64, respawn_delay_ticks: 30, mutation_std: 0.1 },
  temperature: { max_temp: 40, min_temp: -5, cold_threshold: 10, max_cold_decay: 0.5, hot_threshold: 30, max_thirst_accel: 0.3, huddle_range: 2, huddle_warmth_per_agent: 3, agent_body_heat: 2, land_lerp_rate: 0.25, water_heat_capacity: 4, thermal_diffusion_rate: 0.12, river_land_influence: 8 },
  combat: { attack_range: 3, stress_per_attack: 1.5, stress_damage: 0.15, attack_cost: 5 },
  hunger: { decay_rate: 0.04, penalty_start: 30, max_penalty: 0.03, initial: 100 },
  thirst: { decay_rate: 0.025, drink_restore: 40, penalty_start: 20, max_penalty: 0.04, initial: 100 },
  river: { count: 2, width: 3, deep_width: 1, ford_chance: 30, sound_range: 15, sound_decay: 0.05 },
  existence: { decay_per_tick: 0.1, initial: 100 },
  reproduce: { min_existence: 60, min_age: 100, cooldown: 50, parent_cost: 30, child_start: 50, mutation_scale: 0.03 },
  age_death: { max_age: 800, stage1_age: 400, stage1_decay: 0.05, stage2_age: 600, stage2_decay: 0.15, stage3_age: 700, stage3_decay: 0.3 },
};

export function NumberField({ label, value, onChange, min, max, step }: {
  label: string; value: number; onChange: (v: number) => void; min?: number; max?: number; step?: number;
}) {
  return (
    <TextField className="w-full">
      <div className="flex items-center justify-between gap-2 py-0.5">
        <Label className="text-[14px] text-zhi-muted shrink-0">{label}</Label>
        <Input
          type="number"
          value={value}
          onChange={e => onChange(parseFloat(e.target.value) || 0)}
          min={min} max={max} step={step ?? 1}
          className="w-20 text-[12px] text-right"
        />
      </div>
    </TextField>
  );
}

export function ReadOnlyField({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="flex items-center justify-between gap-2 py-0.5">
      <span className="text-[14px] text-zhi-muted">{label}</span>
      <span className="text-[12px] text-zhi-text font-mono text-right">{value}</span>
    </div>
  );
}

export function ConfigSection({ title, defaultExpanded, children }: { title: string; defaultExpanded?: boolean; children: React.ReactNode }) {
  return (
    <Disclosure defaultExpanded={defaultExpanded}>
      <Disclosure.Trigger className="flex items-center gap-1 w-full text-left py-1 group">
        <ChevronDown className="size-3 text-zhi-muted transition-transform group-data-[expanded]:rotate-180" />
        <span className="text-[12px] text-zhi-text font-semibold">{title}</span>
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
  const g = config.grid, co = config.cosmos, te = config.temperature, cb = config.combat;
  const r = config.river, h = config.hunger, th = config.thirst, e = config.existence;
  return (
    <div className="space-y-0.5">
      <ConfigSection title={t('settings.world')} defaultExpanded>
        <NumberField label={t('settings.width')} value={g.width} onChange={v => update('grid', 'width', v)} min={16} max={256} />
        <NumberField label={t('settings.height')} value={g.height} onChange={v => update('grid', 'height', v)} min={16} max={256} />
        <NumberField label={t('settings.agentCount')} value={co.agent_count} onChange={v => update('cosmos', 'agent_count', v)} min={1} max={256} />
        <NumberField label={t('settings.initialFood')} value={g.initial_food} onChange={v => update('grid', 'initial_food', v)} min={0} max={500} />
        <NumberField label={t('settings.initialBigFood')} value={g.initial_big_food} onChange={v => update('grid', 'initial_big_food', v)} min={0} max={100} />
        <NumberField label={t('settings.maxFood')} value={g.max_food} onChange={v => update('grid', 'max_food', v)} min={1} max={1000} />
        <NumberField label={t('settings.foodRespawnInterval')} value={g.food_respawn_interval} onChange={v => update('grid', 'food_respawn_interval', v)} min={0} max={1000} />
      </ConfigSection>

      <ConfigSection title={t('settings.foodEnergySection')}>
        <NumberField label={t('settings.foodEnergy')} value={g.food_energy} onChange={v => update('grid', 'food_energy', v)} min={1} max={200} />
        <NumberField label={t('settings.bigFoodEnergy')} value={g.big_food_energy} onChange={v => update('grid', 'big_food_energy', v)} min={1} max={500} />
        <NumberField label={t('settings.foodDecay')} value={g.food_decay_per_tick} onChange={v => update('grid', 'food_decay_per_tick', v)} min={0.001} max={1} step={0.005} />
        <NumberField label={t('settings.bigFoodDecay')} value={g.big_food_decay_per_tick} onChange={v => update('grid', 'big_food_decay_per_tick', v)} min={0.001} max={1} step={0.005} />
      </ConfigSection>

      <ConfigSection title={t('settings.eating')}>
        <NumberField label={t('settings.foodPerTick')} value={g.food_per_tick_energy} onChange={v => update('grid', 'food_per_tick_energy', v)} min={0.1} max={20} step={0.1} />
        <NumberField label={t('settings.bigFoodPerTick')} value={g.big_food_per_tick_energy} onChange={v => update('grid', 'big_food_per_tick_energy', v)} min={0.1} max={50} step={0.1} />
        <NumberField label={t('settings.corpsePerTick')} value={g.corpse_per_tick_energy} onChange={v => update('grid', 'corpse_per_tick_energy', v)} min={0.1} max={20} step={0.1} />
      </ConfigSection>

      <ConfigSection title={t('settings.temperature')}>
        <NumberField label={t('settings.maxTemp')} value={te.max_temp} onChange={v => update('temperature', 'max_temp', v)} min={10} max={60} />
        <NumberField label={t('settings.minTemp')} value={te.min_temp} onChange={v => update('temperature', 'min_temp', v)} min={-20} max={30} />
        <NumberField label={t('settings.coldThreshold')} value={te.cold_threshold} onChange={v => update('temperature', 'cold_threshold', v)} min={-10} max={40} />
        <NumberField label={t('settings.maxColdDecay')} value={te.max_cold_decay} onChange={v => update('temperature', 'max_cold_decay', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.hotThreshold')} value={te.hot_threshold} onChange={v => update('temperature', 'hot_threshold', v)} min={10} max={50} />
        <NumberField label={t('settings.huddleRange')} value={te.huddle_range} onChange={v => update('temperature', 'huddle_range', v)} min={0} max={10} />
        <NumberField label={t('settings.huddleWarmth')} value={te.huddle_warmth_per_agent} onChange={v => update('temperature', 'huddle_warmth_per_agent', v)} min={0} max={20} step={0.5} />
        <NumberField label={t('settings.agentBodyHeat')} value={te.agent_body_heat} onChange={v => update('temperature', 'agent_body_heat', v)} min={0} max={10} step={0.5} />
        <NumberField label={t('settings.landLerpRate')} value={te.land_lerp_rate} onChange={v => update('temperature', 'land_lerp_rate', v)} min={0.01} max={1} step={0.01} />
        <NumberField label={t('settings.waterHeatCapacity')} value={te.water_heat_capacity} onChange={v => update('temperature', 'water_heat_capacity', v)} min={1} max={20} step={0.5} />
        <NumberField label={t('settings.thermalDiffusion')} value={te.thermal_diffusion_rate} onChange={v => update('temperature', 'thermal_diffusion_rate', v)} min={0} max={0.5} step={0.01} />
        <NumberField label={t('settings.riverLandInfluence')} value={te.river_land_influence} onChange={v => update('temperature', 'river_land_influence', v)} min={0} max={20} />
      </ConfigSection>

      <ConfigSection title={t('settings.combat')}>
        <NumberField label={t('settings.attackRange')} value={cb.attack_range} onChange={v => update('combat', 'attack_range', v)} min={1} max={10} />
        <NumberField label={t('settings.stressPerAttack')} value={cb.stress_per_attack} onChange={v => update('combat', 'stress_per_attack', v)} min={0} max={5} step={0.1} />
        <NumberField label={t('settings.stressDamage')} value={cb.stress_damage} onChange={v => update('combat', 'stress_damage', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.attackCost')} value={cb.attack_cost} onChange={v => update('combat', 'attack_cost', v)} min={0} max={50} step={0.5} />
      </ConfigSection>

      <ConfigSection title={t('settings.river')}>
        <NumberField label={t('settings.riverCount')} value={r.count} onChange={v => update('river', 'count', v)} min={0} max={10} />
        <NumberField label={t('settings.riverWidth')} value={r.width} onChange={v => update('river', 'width', v)} min={1} max={20} />
        <NumberField label={t('settings.riverDeepWidth')} value={r.deep_width} onChange={v => update('river', 'deep_width', v)} min={0} max={10} />
        <NumberField label={t('settings.fordChance')} value={r.ford_chance} onChange={v => update('river', 'ford_chance', v)} min={0} max={100} />
      </ConfigSection>

      <ConfigSection title={t('settings.physiology')}>
        <NumberField label={t('settings.hungerDecay')} value={h.decay_rate} onChange={v => update('hunger', 'decay_rate', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.thirstDecay')} value={th.decay_rate} onChange={v => update('thirst', 'decay_rate', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.drinkRestore')} value={th.drink_restore} onChange={v => update('thirst', 'drink_restore', v)} min={1} max={100} />
        <NumberField label={t('settings.hpDecay')} value={e.decay_per_tick} onChange={v => update('existence', 'decay_per_tick', v)} min={0} max={2} step={0.01} />
      </ConfigSection>
    </div>
  );
}

export function ConfigReadOnly({ config }: { config: ZhiConfig }) {
  const { t } = useT();
  const g = config.grid, co = config.cosmos, te = config.temperature, cb = config.combat;
  const r = config.river, h = config.hunger, th = config.thirst, e = config.existence;
  return (
    <div className="space-y-0.5">
      <ConfigSection title={t('settings.world')} defaultExpanded>
        <ReadOnlyField label={t('settings.width')} value={g.width} />
        <ReadOnlyField label={t('settings.height')} value={g.height} />
        <ReadOnlyField label={t('settings.agentCount')} value={co.agent_count} />
        <ReadOnlyField label={t('settings.initialFood')} value={g.initial_food} />
        <ReadOnlyField label={t('settings.initialBigFood')} value={g.initial_big_food} />
        <ReadOnlyField label={t('settings.maxFood')} value={g.max_food} />
        <ReadOnlyField label={t('settings.foodRespawnInterval')} value={g.food_respawn_interval} />
      </ConfigSection>

      <ConfigSection title={t('settings.foodEnergySection')}>
        <ReadOnlyField label={t('settings.foodEnergy')} value={g.food_energy} />
        <ReadOnlyField label={t('settings.bigFoodEnergy')} value={g.big_food_energy} />
        <ReadOnlyField label={t('settings.foodDecay')} value={g.food_decay_per_tick} />
        <ReadOnlyField label={t('settings.bigFoodDecay')} value={g.big_food_decay_per_tick} />
      </ConfigSection>

      <ConfigSection title={t('settings.eating')}>
        <ReadOnlyField label={t('settings.foodPerTick')} value={g.food_per_tick_energy} />
        <ReadOnlyField label={t('settings.bigFoodPerTick')} value={g.big_food_per_tick_energy} />
        <ReadOnlyField label={t('settings.corpsePerTick')} value={g.corpse_per_tick_energy} />
      </ConfigSection>

      <ConfigSection title={t('settings.temperature')}>
        <ReadOnlyField label={t('settings.maxTemp')} value={te.max_temp} />
        <ReadOnlyField label={t('settings.minTemp')} value={te.min_temp} />
        <ReadOnlyField label={t('settings.coldThreshold')} value={te.cold_threshold} />
        <ReadOnlyField label={t('settings.maxColdDecay')} value={te.max_cold_decay} />
        <ReadOnlyField label={t('settings.hotThreshold')} value={te.hot_threshold} />
        <ReadOnlyField label={t('settings.huddleRange')} value={te.huddle_range} />
        <ReadOnlyField label={t('settings.huddleWarmth')} value={te.huddle_warmth_per_agent} />
        <ReadOnlyField label={t('settings.agentBodyHeat')} value={te.agent_body_heat} />
        <ReadOnlyField label={t('settings.landLerpRate')} value={te.land_lerp_rate} />
        <ReadOnlyField label={t('settings.waterHeatCapacity')} value={te.water_heat_capacity} />
        <ReadOnlyField label={t('settings.thermalDiffusion')} value={te.thermal_diffusion_rate} />
        <ReadOnlyField label={t('settings.riverLandInfluence')} value={te.river_land_influence} />
      </ConfigSection>

      <ConfigSection title={t('settings.combat')}>
        <ReadOnlyField label={t('settings.attackRange')} value={cb.attack_range} />
        <ReadOnlyField label={t('settings.stressPerAttack')} value={cb.stress_per_attack} />
        <ReadOnlyField label={t('settings.stressDamage')} value={cb.stress_damage} />
        <ReadOnlyField label={t('settings.attackCost')} value={cb.attack_cost} />
      </ConfigSection>

      <ConfigSection title={t('settings.river')}>
        <ReadOnlyField label={t('settings.riverCount')} value={r.count} />
        <ReadOnlyField label={t('settings.riverWidth')} value={r.width} />
        <ReadOnlyField label={t('settings.riverDeepWidth')} value={r.deep_width} />
        <ReadOnlyField label={t('settings.fordChance')} value={r.ford_chance} />
      </ConfigSection>

      <ConfigSection title={t('settings.physiology')}>
        <ReadOnlyField label={t('settings.hungerDecay')} value={h.decay_rate} />
        <ReadOnlyField label={t('settings.thirstDecay')} value={th.decay_rate} />
        <ReadOnlyField label={t('settings.drinkRestore')} value={th.drink_restore} />
        <ReadOnlyField label={t('settings.hpDecay')} value={e.decay_per_tick} />
      </ConfigSection>
    </div>
  );
}
