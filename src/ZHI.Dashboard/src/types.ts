export interface WorldMeta {
  name: string
  description: string
  seed: number | null
  created_at: string
  last_run_at: string | null
  status: 'running' | 'stopped' | 'crashed'
  total_generations: number
  total_deaths: number
  config: Record<string, unknown> | null
}

export interface AgentSnapshot {
  id: number
  x: number
  y: number
  energy: number
  stress: number
  water: number
  body_temperature: number
  is_eating: boolean
  is_alive: boolean
  status: string
  last_action: string
  last_signal: number
  chemical_memory: number
  alive_seconds: number
  tick_count: number
  attack_count: number
  eat_count: number
  food_eat_count: number
  corpse_eat_count: number
  emit_count: number
  facing_direction: number
  respawn_count: number
  is_stationary: boolean
}

export interface FoodTile {
  x: number
  y: number
  energy: number
  stage: number  // 0=Seed, 1=Sprout, 2=Adult, 3=Decay
  species: number // 0=Grass, 1=Bush, 2=Tree
  max_energy: number
}

export interface CorpseTile {
  x: number
  y: number
  energy: number
}

export interface WaterCycleData {
  humidity: number
  season_progress: number
  is_wet_season: boolean
}

export interface CosmosState {
  generation: number
  total_deaths: number
  world_day: number
  time_of_day: number
  temperature: number
  agent_count: number
  plant_count: number
  grid_width: number
  grid_height: number
  agents: AgentSnapshot[]
  food: FoodTile[]
  corpses: CorpseTile[]
  river: number[]
  scent: number[]
  food_scent: number[]
  temperature_grid: number[]
  chemical_field: number[]
  height_map: number[]
  slope: number[]
  river_flow: number[]
  surface_water: number[]
  groundwater: number[]
  nutrient: number[]
  permeability: number[]
  pressure: number[]
  wind_x: number[]
  wind_y: number[]
  sunlight: number[]
  biome: number[]
  water_cycle: WaterCycleData
}

export type WorldEventType = 'eat' | 'attack' | 'death' | 'reproduce' | 'signal' | 'respawn' | 'energyloss'

export interface WorldEvent {
  type: WorldEventType
  agent_id: number
  target_id?: number
  child_id?: number
  food_type?: string
  signal_value?: number
  cause?: string
  value: number
  tick: number
}

export interface LogMessage {
  type: 'log'
  time: string
  message: string
}

export interface EnergySource {
  food_pct: number
  corpse_pct: number
}

export interface CosmosStats {
  attack_rate: number
  food_eaten: number
  corpses_eaten: number
  energy_source: EnergySource
}

export interface StatsData {
  total_generations: number
  total_deaths: number
  suicide_rate_all: number
  suicide_rate_recent_10: number
  avg_alive_seconds_all: number
  avg_alive_seconds_recent_10: number
  avg_energy_at_death: number
  avg_water_at_death: number
  avg_temperature_at_death: number
  avg_attacks_per_life: number
  avg_eats_per_life: number
  avg_emits_per_life: number
  night_death_rate: number
  cause_distribution: Record<string, number>
  generations: { generation: number; cause: string; alive_seconds: number }[]
}
