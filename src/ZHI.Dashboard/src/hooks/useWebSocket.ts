import { useEffect, useRef, useState, useCallback, useReducer } from 'react';
import type { AgentSnapshot, FoodTile, CorpseTile, CosmosStats, WaterCycleData } from '../types';

const WS_URL = import.meta.env.DEV
  ? 'ws://localhost:8088/ws'
  : `${location.protocol === 'https:' ? 'wss:' : 'ws:'}//${location.host}/ws`;

interface WsState {
  connected: boolean;
  generation: number;
  totalDeaths: number;
  worldDay: number;
  timeOfDay: number;
  temperature: number;
  gridW: number;
  gridH: number;
  agents: AgentSnapshot[];
  food: FoodTile[];
  corpses: CorpseTile[];
  river: number[];
  scent: number[];
  foodScent: number[];
  temperatureGrid: number[];
  chemicalField: number[];
  terrain: number[];
  terrainTtl: number[];
  riverFlow: number[];
  surfaceWater: number[];
  groundwater: number[];
  nutrient: number[];
  waterCycle: WaterCycleData | null;
  plantCount: number;
  stats: CosmosStats | null;
}

const INIT: WsState = {
  connected: false,
  generation: 1,
  totalDeaths: 0,
  worldDay: 1,
  timeOfDay: 0,
  temperature: 20,
  gridW: 64,
  gridH: 64,
  agents: [],
  food: [],
  corpses: [],
  river: [],
  scent: [],
  foodScent: [],
  temperatureGrid: [],
  chemicalField: [],
  terrain: [],
  terrainTtl: [],
  riverFlow: [],
  surfaceWater: [],
  groundwater: [],
  nutrient: [],
  waterCycle: null,
  plantCount: 0,
  stats: null,
};

type Action = { type: 'cosmos'; data: Record<string, unknown> } | { type: 'connected'; value: boolean };

function reducer(state: WsState, action: Action): WsState {
  switch (action.type) {
    case 'connected':
      return { ...state, connected: action.value };
    case 'cosmos': {
      const d = action.data;
      return {
        ...state,
        generation: (d.generation as number) ?? state.generation,
        totalDeaths: (d.total_deaths as number) ?? state.totalDeaths,
        worldDay: (d.world_day as number) ?? state.worldDay,
        timeOfDay: (d.time_of_day as number) ?? state.timeOfDay,
        temperature: (d.temperature as number) ?? state.temperature,
        gridW: (d.grid_width as number) || state.gridW,
        gridH: (d.grid_height as number) || state.gridH,
        agents: (d.agents as AgentSnapshot[]) ?? state.agents,
        food: (d.food as FoodTile[]) ?? state.food,
        corpses: (d.corpses as CorpseTile[]) ?? state.corpses,
        river: (d.river as number[]) ?? state.river,
        scent: (d.scent as number[]) ?? state.scent,
        foodScent: (d.food_scent as number[]) ?? state.foodScent,
        temperatureGrid: (d.temperature_grid as number[]) ?? state.temperatureGrid,
        chemicalField: (d.chemical_field as number[]) ?? state.chemicalField,
        terrain: (d.terrain as number[]) ?? state.terrain,
        terrainTtl: (d.terrain_ttl as number[]) ?? state.terrainTtl,
        riverFlow: (d.river_flow as number[]) ?? state.riverFlow,
        surfaceWater: (d.surface_water as number[]) ?? state.surfaceWater,
        groundwater: (d.groundwater as number[]) ?? state.groundwater,
        nutrient: (d.nutrient as number[]) ?? state.nutrient,
        waterCycle: (d.water_cycle as WaterCycleData) ?? state.waterCycle,
        plantCount: (d.plant_count as number) ?? state.plantCount,
        stats: (d.stats as CosmosStats) ?? state.stats,
      };
    }
  }
}

export function useWebSocket() {
  const [state, dispatch] = useReducer(reducer, INIT);
  const [connected, setConnected] = useState(false);
  const wsRef = useRef<WebSocket | null>(null);
  const reconnectRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;

    const ws = new WebSocket(WS_URL);
    wsRef.current = ws;

    ws.onopen = () => {
      setConnected(true);
      dispatch({ type: 'connected', value: true });
    };

    ws.onclose = () => {
      setConnected(false);
      dispatch({ type: 'connected', value: false });
      reconnectRef.current = setTimeout(connect, 2000);
    };

    ws.onerror = () => ws.close();

    ws.onmessage = (e) => {
      const msg = JSON.parse(e.data);
      if (msg.type === 'cosmos') {
        dispatch({ type: 'cosmos', data: msg.data as Record<string, unknown> });
      }
    };
  }, []);

  useEffect(() => {
    connect();
    return () => {
      clearTimeout(reconnectRef.current);
      wsRef.current?.close();
    };
  }, [connect]);

  return {
    connected,
    generation: state.generation,
    totalDeaths: state.totalDeaths,
    worldDay: state.worldDay,
    timeOfDay: state.timeOfDay,
    temperature: state.temperature,
    gridW: state.gridW,
    gridH: state.gridH,
    agents: state.agents,
    food: state.food,
    corpses: state.corpses,
    river: state.river,
    scent: state.scent,
    foodScent: state.foodScent,
    temperatureGrid: state.temperatureGrid,
    chemicalField: state.chemicalField,
    terrain: state.terrain,
    terrainTtl: state.terrainTtl,
    riverFlow: state.riverFlow,
    surfaceWater: state.surfaceWater,
    groundwater: state.groundwater,
    nutrient: state.nutrient,
    waterCycle: state.waterCycle,
    plantCount: state.plantCount,
    stats: state.stats,
  };
}
