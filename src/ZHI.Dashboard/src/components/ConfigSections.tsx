import { TextField, Label, Input, Disclosure } from '@heroui/react';
import { ChevronDown } from '@gravity-ui/icons';
import { useT } from '../i18n/I18nContext';

export interface ZhiConfig {
  grid: { width: number; height: number; max_agents: number; initial_food: number; initial_big_food: number; food_energy: number; big_food_energy: number; food_decay_per_tick: number; big_food_decay_per_tick: number; max_food: number; food_respawn_interval: number; food_per_tick_energy: number; big_food_per_tick_energy: number; corpse_per_tick_energy: number };
  cosmos: { agent_count: number; elite_count: number; mutation_rate: number; mutation_std: number; mutation_rate_min: number; mutation_decay_generations: number; respawn_delay_ticks: number };
  temperature: { max_temp: number; min_temp: number; cold_threshold: number; max_cold_decay: number; hot_threshold: number; max_thirst_accel: number; huddle_range: number; huddle_warmth_per_agent: number; agent_body_heat: number; land_lerp_rate: number; water_heat_capacity: number; thermal_diffusion_rate: number; river_land_influence: number; hypothermia_threshold: number; hypothermia_max_damage: number; water_cooling_mult: number; deep_water_extra_cold: number; min_body_temp: number };
  combat: { attack_range: number; stress_per_attack: number; stress_damage: number; attack_cost: number };
  hunger: { initial: number; decay_rate: number; penalty_start: number; max_penalty: number };
  thirst: { initial: number; decay_rate: number; drink_restore: number; penalty_start: number; max_penalty: number };
  river: { count: number; width: number; deep_width: number; ford_chance: number; sound_range: number; sound_decay: number };
  existence: { initial: number; decay_per_tick: number };
  reproduce: { min_existence: number; min_age: number; cooldown: number; parent_cost: number; child_start: number; mutation_scale: number };
  age_death: { max_age: number; stage1_age: number; stage1_decay: number; stage2_age: number; stage2_decay: number; stage3_age: number; stage3_decay: number };
  signal: { cost: number; num_values: number; wave_radius: number };
  scent: { deposit_amount: number; decay_rate: number; diffusion_rate: number };
  food_scent: { decay_rate: number; diffusion_rate: number; small_food_emission: number; big_food_emission: number; spread_radius: number };
  network: { learning_rate: number; gamma: number };
  corpse: { energy: number; decay_per_tick: number; scent_amount: number };
  stamina: { max_stamina: number; move_cost: number; attack_cost: number; push_cost: number; terraform_cost: number; signal_cost: number; shove_cost: number; pull_cost: number; shallow_water_move_extra: number; deep_water_move_extra: number; deep_water_climb_extra: number; base_recovery: number; stationary_recovery_bonus: number; low_stamina_threshold: number; stationary_ticks_required: number; stationary_damage_mult: number; stationary_self_heat: number; stationary_neighbor_heat: number; stationary_hp_recovery_bonus: number };
}

export const DEFAULT_CONFIG: ZhiConfig = {
  grid: { width: 64, height: 64, max_agents: 512, initial_food: 70, initial_big_food: 10, food_energy: 15, big_food_energy: 80, food_decay_per_tick: 0.075, big_food_decay_per_tick: 0.2, max_food: 100, food_respawn_interval: 10, food_per_tick_energy: 1.0, big_food_per_tick_energy: 2.0, corpse_per_tick_energy: 1.0 },
  cosmos: { agent_count: 64, elite_count: 2, mutation_rate: 0.1, mutation_std: 0.02, mutation_rate_min: 0.02, mutation_decay_generations: 100, respawn_delay_ticks: 25 },
  temperature: { max_temp: 35, min_temp: 5, cold_threshold: 15, max_cold_decay: 0.15, hot_threshold: 30, max_thirst_accel: 1.5, huddle_range: 2, huddle_warmth_per_agent: 3, agent_body_heat: 2, land_lerp_rate: 0.25, water_heat_capacity: 4, thermal_diffusion_rate: 0.12, river_land_influence: 8, hypothermia_threshold: 33, hypothermia_max_damage: 0.08, water_cooling_mult: 2, deep_water_extra_cold: 3, min_body_temp: 26 },
  combat: { attack_range: 1, stress_per_attack: 0.5, stress_damage: 0.1, attack_cost: 1.0 },
  hunger: { initial: 100, decay_rate: 0.05, penalty_start: 60, max_penalty: 0.25 },
  thirst: { initial: 100, decay_rate: 0.1, drink_restore: 40, penalty_start: 70, max_penalty: 0.8 },
  river: { count: 1, width: 5, deep_width: 1, ford_chance: 25, sound_range: 10, sound_decay: 0.9 },
  existence: { initial: 100, decay_per_tick: 0.1 },
  reproduce: { min_existence: 80, min_age: 200, cooldown: 500, parent_cost: 40, child_start: 40, mutation_scale: 0.3 },
  age_death: { max_age: 8000, stage1_age: 5000, stage1_decay: 0.02, stage2_age: 6000, stage2_decay: 0.05, stage3_age: 7000, stage3_decay: 0.1 },
  signal: { cost: 0.25, num_values: 4, wave_radius: 4 },
  scent: { deposit_amount: 1.0, decay_rate: 0.95, diffusion_rate: 0.1 },
  food_scent: { decay_rate: 0.85, diffusion_rate: 0.08, small_food_emission: 0.3, big_food_emission: 1.0, spread_radius: 2 },
  network: { learning_rate: 0.001, gamma: 0.99 },
  corpse: { energy: 20, decay_per_tick: 0.067, scent_amount: 0.5 },
  stamina: { max_stamina: 100, move_cost: 0.5, attack_cost: 8, push_cost: 12, terraform_cost: 20, signal_cost: 3, shove_cost: 15, pull_cost: 10, shallow_water_move_extra: 1, deep_water_move_extra: 2.5, deep_water_climb_extra: 1, base_recovery: 1, stationary_recovery_bonus: 2, low_stamina_threshold: 10, stationary_ticks_required: 5, stationary_damage_mult: 1.2, stationary_self_heat: 3, stationary_neighbor_heat: 2, stationary_hp_recovery_bonus: 0.1 },
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
  const re = config.reproduce, ad = config.age_death, si = config.signal;
  const co2 = config.corpse, st = config.stamina;
  return (
    <div className="space-y-0.5">
      <ConfigSection title={t('settings.world')} defaultExpanded>
        <NumberField label={t('settings.width')} value={g.width} onChange={v => update('grid', 'width', v)} min={16} max={256} />
        <NumberField label={t('settings.height')} value={g.height} onChange={v => update('grid', 'height', v)} min={16} max={256} />
        <NumberField label={t('settings.maxAgents')} value={g.max_agents} onChange={v => update('grid', 'max_agents', v)} min={1} max={2048} />
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

      <ConfigSection title={t('settings.physiology')}>
        <NumberField label={t('settings.hungerDecay')} value={h.decay_rate} onChange={v => update('hunger', 'decay_rate', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.thirstDecay')} value={th.decay_rate} onChange={v => update('thirst', 'decay_rate', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.drinkRestore')} value={th.drink_restore} onChange={v => update('thirst', 'drink_restore', v)} min={1} max={100} />
        <NumberField label={t('settings.hungerPenaltyStart')} value={h.penalty_start} onChange={v => update('hunger', 'penalty_start', v)} min={0} max={100} />
        <NumberField label={t('settings.thirstPenaltyStart')} value={th.penalty_start} onChange={v => update('thirst', 'penalty_start', v)} min={0} max={100} />
        <NumberField label={t('settings.hpDecay')} value={e.decay_per_tick} onChange={v => update('existence', 'decay_per_tick', v)} min={0} max={2} step={0.01} />
      </ConfigSection>

      <ConfigSection title={t('settings.reproduction')}>
        <NumberField label={t('settings.minExistence')} value={re.min_existence} onChange={v => update('reproduce', 'min_existence', v)} min={0} max={100} />
        <NumberField label={t('settings.minAge')} value={re.min_age} onChange={v => update('reproduce', 'min_age', v)} min={0} max={2000} />
        <NumberField label={t('settings.reproCooldown')} value={re.cooldown} onChange={v => update('reproduce', 'cooldown', v)} min={0} max={5000} />
        <NumberField label={t('settings.parentCost')} value={re.parent_cost} onChange={v => update('reproduce', 'parent_cost', v)} min={0} max={100} />
        <NumberField label={t('settings.childStart')} value={re.child_start} onChange={v => update('reproduce', 'child_start', v)} min={1} max={100} />
        <NumberField label={t('settings.mutationStd')} value={co.mutation_std} onChange={v => update('cosmos', 'mutation_std', v)} min={0} max={0.5} step={0.005} />
        <NumberField label={t('settings.mutationScale')} value={re.mutation_scale} onChange={v => update('reproduce', 'mutation_scale', v)} min={0} max={1} step={0.01} />
      </ConfigSection>

      <ConfigSection title={t('settings.temperature')}>
        <NumberField label={t('settings.maxTemp')} value={te.max_temp} onChange={v => update('temperature', 'max_temp', v)} min={10} max={60} />
        <NumberField label={t('settings.minTemp')} value={te.min_temp} onChange={v => update('temperature', 'min_temp', v)} min={-20} max={30} />
        <NumberField label={t('settings.coldThreshold')} value={te.cold_threshold} onChange={v => update('temperature', 'cold_threshold', v)} min={-10} max={40} />
        <NumberField label={t('settings.maxColdDecay')} value={te.max_cold_decay} onChange={v => update('temperature', 'max_cold_decay', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.hotThreshold')} value={te.hot_threshold} onChange={v => update('temperature', 'hot_threshold', v)} min={10} max={50} />
        <NumberField label={t('settings.maxThirstAccel')} value={te.max_thirst_accel} onChange={v => update('temperature', 'max_thirst_accel', v)} min={1} max={5} step={0.1} />
        <NumberField label={t('settings.agentBodyHeat')} value={te.agent_body_heat} onChange={v => update('temperature', 'agent_body_heat', v)} min={0} max={10} step={0.5} />
        <NumberField label={t('settings.huddleRange')} value={te.huddle_range} onChange={v => update('temperature', 'huddle_range', v)} min={0} max={10} />
        <NumberField label={t('settings.huddleWarmth')} value={te.huddle_warmth_per_agent} onChange={v => update('temperature', 'huddle_warmth_per_agent', v)} min={0} max={20} step={0.5} />
        <NumberField label={t('settings.hypothermiaThreshold')} value={te.hypothermia_threshold} onChange={v => update('temperature', 'hypothermia_threshold', v)} min={20} max={40} />
        <NumberField label={t('settings.hypothermiaMaxDamage')} value={te.hypothermia_max_damage} onChange={v => update('temperature', 'hypothermia_max_damage', v)} min={0} max={0.5} step={0.01} />
        <NumberField label={t('settings.landLerpRate')} value={te.land_lerp_rate} onChange={v => update('temperature', 'land_lerp_rate', v)} min={0.01} max={1} step={0.01} />
        <NumberField label={t('settings.waterHeatCapacity')} value={te.water_heat_capacity} onChange={v => update('temperature', 'water_heat_capacity', v)} min={1} max={20} step={0.5} />
        <NumberField label={t('settings.thermalDiffusion')} value={te.thermal_diffusion_rate} onChange={v => update('temperature', 'thermal_diffusion_rate', v)} min={0} max={0.5} step={0.01} />
        <NumberField label={t('settings.waterCoolingMult')} value={te.water_cooling_mult} onChange={v => update('temperature', 'water_cooling_mult', v)} min={0} max={10} step={0.5} />
        <NumberField label={t('settings.deepWaterExtraCold')} value={te.deep_water_extra_cold} onChange={v => update('temperature', 'deep_water_extra_cold', v)} min={0} max={20} />
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
        <NumberField label={t('settings.soundRange')} value={r.sound_range} onChange={v => update('river', 'sound_range', v)} min={0} max={30} />
      </ConfigSection>

      <ConfigSection title={t('settings.signal')}>
        <NumberField label={t('settings.signalCost')} value={si.cost} onChange={v => update('signal', 'cost', v)} min={0} max={5} step={0.05} />
        <NumberField label={t('settings.signalNumValues')} value={si.num_values} onChange={v => update('signal', 'num_values', v)} min={1} max={8} />
        <NumberField label={t('settings.signalWaveRadius')} value={si.wave_radius} onChange={v => update('signal', 'wave_radius', v)} min={1} max={10} />
      </ConfigSection>

      <ConfigSection title={t('settings.stamina')}>
        <NumberField label={t('settings.maxStamina')} value={st.max_stamina} onChange={v => update('stamina', 'max_stamina', v)} min={10} max={200} />
        <NumberField label={t('settings.moveCost')} value={st.move_cost} onChange={v => update('stamina', 'move_cost', v)} min={0} max={10} step={0.1} />
        <NumberField label={t('settings.staminaAttackCost')} value={st.attack_cost} onChange={v => update('stamina', 'attack_cost', v)} min={0} max={50} step={0.5} />
        <NumberField label={t('settings.pushCost')} value={st.push_cost} onChange={v => update('stamina', 'push_cost', v)} min={0} max={50} step={0.5} />
        <NumberField label={t('settings.terraformCost')} value={st.terraform_cost} onChange={v => update('stamina', 'terraform_cost', v)} min={0} max={50} step={0.5} />
        <NumberField label={t('settings.staminaSignalCost')} value={st.signal_cost} onChange={v => update('stamina', 'signal_cost', v)} min={0} max={20} step={0.5} />
        <NumberField label={t('settings.baseRecovery')} value={st.base_recovery} onChange={v => update('stamina', 'base_recovery', v)} min={0} max={10} step={0.1} />
        <NumberField label={t('settings.lowStaminaThreshold')} value={st.low_stamina_threshold} onChange={v => update('stamina', 'low_stamina_threshold', v)} min={0} max={50} />
        <NumberField label={t('settings.stationaryTicksRequired')} value={st.stationary_ticks_required} onChange={v => update('stamina', 'stationary_ticks_required', v)} min={1} max={50} />
      </ConfigSection>

      <ConfigSection title={t('settings.corpse')}>
        <NumberField label={t('settings.corpseEnergy')} value={co2.energy} onChange={v => update('corpse', 'energy', v)} min={1} max={100} />
        <NumberField label={t('settings.corpseDecay')} value={co2.decay_per_tick} onChange={v => update('corpse', 'decay_per_tick', v)} min={0.001} max={1} step={0.005} />
      </ConfigSection>

      <ConfigSection title={t('settings.ageDeath')}>
        <NumberField label={t('settings.maxAge')} value={ad.max_age} onChange={v => update('age_death', 'max_age', v)} min={100} max={50000} />
        <NumberField label={t('settings.stage1Age')} value={ad.stage1_age} onChange={v => update('age_death', 'stage1_age', v)} min={100} max={50000} />
        <NumberField label={t('settings.stage1Decay')} value={ad.stage1_decay} onChange={v => update('age_death', 'stage1_decay', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.stage2Age')} value={ad.stage2_age} onChange={v => update('age_death', 'stage2_age', v)} min={100} max={50000} />
        <NumberField label={t('settings.stage2Decay')} value={ad.stage2_decay} onChange={v => update('age_death', 'stage2_decay', v)} min={0} max={1} step={0.01} />
        <NumberField label={t('settings.stage3Age')} value={ad.stage3_age} onChange={v => update('age_death', 'stage3_age', v)} min={100} max={50000} />
        <NumberField label={t('settings.stage3Decay')} value={ad.stage3_decay} onChange={v => update('age_death', 'stage3_decay', v)} min={0} max={1} step={0.01} />
      </ConfigSection>
    </div>
  );
}

export function ConfigReadOnly({ config }: { config: ZhiConfig }) {
  const { t } = useT();
  const g = config.grid, co = config.cosmos, te = config.temperature, cb = config.combat;
  const r = config.river, h = config.hunger, th = config.thirst, e = config.existence;
  const re = config.reproduce, ad = config.age_death, si = config.signal;
  const sc = config.scent, fs = config.food_scent, nw = config.network;
  const co2 = config.corpse, st = config.stamina;
  return (
    <div className="space-y-0.5">
      <ConfigSection title={t('settings.world')} defaultExpanded>
        <ReadOnlyField label={t('settings.width')} value={g.width} />
        <ReadOnlyField label={t('settings.height')} value={g.height} />
        <ReadOnlyField label={t('settings.maxAgents')} value={g.max_agents} />
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

      <ConfigSection title={t('settings.physiology')}>
        <ReadOnlyField label={t('settings.hungerDecay')} value={h.decay_rate} />
        <ReadOnlyField label={t('settings.thirstDecay')} value={th.decay_rate} />
        <ReadOnlyField label={t('settings.drinkRestore')} value={th.drink_restore} />
        <ReadOnlyField label={t('settings.hungerPenaltyStart')} value={h.penalty_start} />
        <ReadOnlyField label={t('settings.thirstPenaltyStart')} value={th.penalty_start} />
        <ReadOnlyField label={t('settings.hpDecay')} value={e.decay_per_tick} />
      </ConfigSection>

      <ConfigSection title={t('settings.reproduction')}>
        <ReadOnlyField label={t('settings.minExistence')} value={re.min_existence} />
        <ReadOnlyField label={t('settings.minAge')} value={re.min_age} />
        <ReadOnlyField label={t('settings.reproCooldown')} value={re.cooldown} />
        <ReadOnlyField label={t('settings.parentCost')} value={re.parent_cost} />
        <ReadOnlyField label={t('settings.childStart')} value={re.child_start} />
        <ReadOnlyField label={t('settings.mutationRate')} value={co.mutation_rate} />
        <ReadOnlyField label={t('settings.mutationStd')} value={co.mutation_std} />
        <ReadOnlyField label={t('settings.mutationScale')} value={re.mutation_scale} />
      </ConfigSection>

      <ConfigSection title={t('settings.temperature')}>
        <ReadOnlyField label={t('settings.maxTemp')} value={te.max_temp} />
        <ReadOnlyField label={t('settings.minTemp')} value={te.min_temp} />
        <ReadOnlyField label={t('settings.coldThreshold')} value={te.cold_threshold} />
        <ReadOnlyField label={t('settings.maxColdDecay')} value={te.max_cold_decay} />
        <ReadOnlyField label={t('settings.hotThreshold')} value={te.hot_threshold} />
        <ReadOnlyField label={t('settings.maxThirstAccel')} value={te.max_thirst_accel} />
        <ReadOnlyField label={t('settings.agentBodyHeat')} value={te.agent_body_heat} />
        <ReadOnlyField label={t('settings.huddleRange')} value={te.huddle_range} />
        <ReadOnlyField label={t('settings.huddleWarmth')} value={te.huddle_warmth_per_agent} />
        <ReadOnlyField label={t('settings.hypothermiaThreshold')} value={te.hypothermia_threshold} />
        <ReadOnlyField label={t('settings.hypothermiaMaxDamage')} value={te.hypothermia_max_damage} />
        <ReadOnlyField label={t('settings.landLerpRate')} value={te.land_lerp_rate} />
        <ReadOnlyField label={t('settings.waterHeatCapacity')} value={te.water_heat_capacity} />
        <ReadOnlyField label={t('settings.thermalDiffusion')} value={te.thermal_diffusion_rate} />
        <ReadOnlyField label={t('settings.waterCoolingMult')} value={te.water_cooling_mult} />
        <ReadOnlyField label={t('settings.deepWaterExtraCold')} value={te.deep_water_extra_cold} />
        <ReadOnlyField label={t('settings.riverLandInfluence')} value={te.river_land_influence} />
        <ReadOnlyField label={t('settings.minBodyTemp')} value={te.min_body_temp} />
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
        <ReadOnlyField label={t('settings.soundRange')} value={r.sound_range} />
        <ReadOnlyField label={t('settings.soundDecay')} value={r.sound_decay} />
      </ConfigSection>

      <ConfigSection title={t('settings.signal')}>
        <ReadOnlyField label={t('settings.signalCost')} value={si.cost} />
        <ReadOnlyField label={t('settings.signalNumValues')} value={si.num_values} />
        <ReadOnlyField label={t('settings.signalWaveRadius')} value={si.wave_radius} />
      </ConfigSection>

      <ConfigSection title={t('settings.scent')}>
        <ReadOnlyField label={t('settings.scentDepositAmount')} value={sc.deposit_amount} />
        <ReadOnlyField label={t('settings.scentDecayRate')} value={sc.decay_rate} />
        <ReadOnlyField label={t('settings.scentDiffusionRate')} value={sc.diffusion_rate} />
      </ConfigSection>

      <ConfigSection title={t('settings.foodScent')}>
        <ReadOnlyField label={t('settings.foodScentDecayRate')} value={fs.decay_rate} />
        <ReadOnlyField label={t('settings.foodScentDiffusionRate')} value={fs.diffusion_rate} />
        <ReadOnlyField label={t('settings.smallFoodEmission')} value={fs.small_food_emission} />
        <ReadOnlyField label={t('settings.bigFoodEmission')} value={fs.big_food_emission} />
        <ReadOnlyField label={t('settings.foodScentSpreadRadius')} value={fs.spread_radius} />
      </ConfigSection>

      <ConfigSection title={t('settings.network')}>
        <ReadOnlyField label={t('settings.learningRate')} value={nw.learning_rate} />
        <ReadOnlyField label={t('settings.gamma')} value={nw.gamma} />
      </ConfigSection>

      <ConfigSection title={t('settings.corpse')}>
        <ReadOnlyField label={t('settings.corpseEnergy')} value={co2.energy} />
        <ReadOnlyField label={t('settings.corpseDecay')} value={co2.decay_per_tick} />
        <ReadOnlyField label={t('settings.corpseScentAmount')} value={co2.scent_amount} />
      </ConfigSection>

      <ConfigSection title={t('settings.stamina')}>
        <ReadOnlyField label={t('settings.maxStamina')} value={st.max_stamina} />
        <ReadOnlyField label={t('settings.moveCost')} value={st.move_cost} />
        <ReadOnlyField label={t('settings.staminaAttackCost')} value={st.attack_cost} />
        <ReadOnlyField label={t('settings.pushCost')} value={st.push_cost} />
        <ReadOnlyField label={t('settings.terraformCost')} value={st.terraform_cost} />
        <ReadOnlyField label={t('settings.staminaSignalCost')} value={st.signal_cost} />
        <ReadOnlyField label={t('settings.shoveCost')} value={st.shove_cost} />
        <ReadOnlyField label={t('settings.pullCost')} value={st.pull_cost} />
        <ReadOnlyField label={t('settings.shallowWaterExtra')} value={st.shallow_water_move_extra} />
        <ReadOnlyField label={t('settings.deepWaterExtra')} value={st.deep_water_move_extra} />
        <ReadOnlyField label={t('settings.baseRecovery')} value={st.base_recovery} />
        <ReadOnlyField label={t('settings.stationaryRecoveryBonus')} value={st.stationary_recovery_bonus} />
        <ReadOnlyField label={t('settings.lowStaminaThreshold')} value={st.low_stamina_threshold} />
        <ReadOnlyField label={t('settings.stationaryTicksRequired')} value={st.stationary_ticks_required} />
        <ReadOnlyField label={t('settings.stationaryDamageMult')} value={st.stationary_damage_mult} />
        <ReadOnlyField label={t('settings.stationarySelfHeat')} value={st.stationary_self_heat} />
        <ReadOnlyField label={t('settings.stationaryNeighborHeat')} value={st.stationary_neighbor_heat} />
        <ReadOnlyField label={t('settings.stationaryHpRecovery')} value={st.stationary_hp_recovery_bonus} />
      </ConfigSection>

      <ConfigSection title={t('settings.ageDeath')}>
        <ReadOnlyField label={t('settings.maxAge')} value={ad.max_age} />
        <ReadOnlyField label={t('settings.stage1Age')} value={ad.stage1_age} />
        <ReadOnlyField label={t('settings.stage1Decay')} value={ad.stage1_decay} />
        <ReadOnlyField label={t('settings.stage2Age')} value={ad.stage2_age} />
        <ReadOnlyField label={t('settings.stage2Decay')} value={ad.stage2_decay} />
        <ReadOnlyField label={t('settings.stage3Age')} value={ad.stage3_age} />
        <ReadOnlyField label={t('settings.stage3Decay')} value={ad.stage3_decay} />
      </ConfigSection>
    </div>
  );
}
