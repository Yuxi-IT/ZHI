export interface AgentSnapshot {
  id: number
  x: number
  y: number
  existence: number
  stress: number
  is_alive: boolean
  status: string
  last_action: string
  last_signal: number
  alive_seconds: number
  tick_count: number
  attack_count: number
  eat_count: number
  signal_count: number
}

export interface FoodTile {
  x: number
  y: number
  ttl: number
}

export interface CosmosState {
  generation: number
  total_deaths: number
  agent_count: number
  grid_width: number
  grid_height: number
  agents: AgentSnapshot[]
  food: FoodTile[]
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
