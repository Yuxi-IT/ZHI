export interface AgentSnapshot {
  id: number
  x: number
  y: number
  existence: number
  stress: number
  thirst: number
  is_alive: boolean
  status: string
  last_action: string
  last_signal: number
  signal_memory: number[]
  alive_seconds: number
  tick_count: number
  attack_count: number
  eat_count: number
  food_eat_count: number
  bigfood_eat_count: number
  corpse_eat_count: number
  signal_count: number
  facing_direction: number  // 0=up, 1=down, 2=left, 3=right
  is_hiding: boolean
}

export interface FoodTile {
  x: number
  y: number
  width: number
  height: number
  ttl: number
  energy: number
  is_big: boolean
}

export interface CorpseTile {
  x: number
  y: number
  ttl: number
  energy: number
}

export interface CosmosState {
  generation: number
  total_deaths: number
  agent_count: number
  grid_width: number
  grid_height: number
  agents: AgentSnapshot[]
  food: FoodTile[]
  corpses: CorpseTile[]
  river: number[]  // flat array: 0=land, 1=shallow, 2=deep (row-major)
  bush: number[]   // flat array: 0=empty, 1=bush (row-major)
}

export interface LogMessage {
  type: 'log'
  time: string
  message: string
}

export interface GenerationStat {
  generation: number
  cause: string
  alive_seconds: number
}

export interface StatsData {
  total_generations: number
  total_deaths: number
  suicide_rate_all: number
  suicide_rate_recent_10: number
  avg_alive_seconds_all: number
  avg_alive_seconds_recent_10: number
  cause_distribution: Record<string, number>
  generations: GenerationStat[]
}

export type WsMessage =
  | ({ type: 'cosmos'; data: CosmosState })
  | LogMessage

export type WorldEventType = 'eat' | 'attack' | 'death' | 'reproduce' | 'hide_enter' | 'hide_exit' | 'signal'

export interface WorldEvent {
  type: WorldEventType
  agent_id: number
  target_id?: number
  child_id?: number
  food_type?: string
  signal_value?: number
  value: number
  tick: number
}

export interface EnergySource {
  food_pct: number
  bigfood_pct: number
  corpse_pct: number
}

export interface CosmosStats {
  attack_rate: number
  hide_usage_rate: number
  food_eaten: number
  bigfood_eaten: number
  corpses_eaten: number
  energy_source: EnergySource
}
