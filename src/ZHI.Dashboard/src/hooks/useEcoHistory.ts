import { useRef, useCallback } from 'react'
import type { AgentSnapshot, FoodTile, CorpseTile } from '../types'

export interface EcoDataPoint {
  t: number          // tick index (monotonic counter)
  alive: number
  food: number
  corpses: number
  energy: number
  births: number
  deaths: number
}

const MAX_POINTS = 300

export function useEcoHistory() {
  const historyRef = useRef<EcoDataPoint[]>([])
  const tickRef = useRef(0)
  const prevAliveRef = useRef(0)
  const prevGenRef = useRef(0)

  const record = useCallback((agents: AgentSnapshot[], food: FoodTile[], corpses: CorpseTile[], generation: number) => {
    const alive = agents.filter(a => a.is_alive).length
    const foodCount = food.length
    const corpseCount = corpses.length
    const energy = agents.reduce((sum, a) => a.is_alive ? sum + Math.max(0, a.existence) : sum, 0)
      + food.reduce((sum, f) => sum + f.energy, 0)
      + corpses.reduce((sum, c) => sum + c.energy, 0)

    // Reset on generation change
    if (generation !== prevGenRef.current) {
      prevGenRef.current = generation
      prevAliveRef.current = alive
      historyRef.current = []
      tickRef.current = 0
    }

    const prevAlive = prevAliveRef.current
    let births = 0
    let deaths = 0
    if (alive > prevAlive) births = alive - prevAlive
    else if (alive < prevAlive) deaths = prevAlive - alive

    prevAliveRef.current = alive
    tickRef.current++

    const point: EcoDataPoint = {
      t: tickRef.current,
      alive,
      food: foodCount,
      corpses: corpseCount,
      energy,
      births,
      deaths,
    }

    historyRef.current.push(point)
    if (historyRef.current.length > MAX_POINTS) {
      historyRef.current = historyRef.current.slice(-MAX_POINTS)
    }
  }, [])

  return { history: historyRef.current, record }
}
