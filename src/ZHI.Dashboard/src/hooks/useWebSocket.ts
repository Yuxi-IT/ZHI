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
  heightMap: number[];
  slope: number[];
  riverFlow: number[];
  surfaceWater: number[];
  groundwater: number[];
  nutrient: number[];
  permeability: number[];
  pressure: number[];
  windX: number[];
  windY: number[];
  sunlight: number[];
  biome: number[];
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
  heightMap: [],
  slope: [],
  riverFlow: [],
  surfaceWater: [],
  groundwater: [],
  nutrient: [],
  permeability: [],
  pressure: [],
  windX: [],
  windY: [],
  sunlight: [],
  biome: [],
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
        heightMap: (d.height_map as number[]) ?? state.heightMap,
        slope: (d.slope as number[]) ?? state.slope,
        riverFlow: (d.river_flow as number[]) ?? state.riverFlow,
        surfaceWater: (d.surface_water as number[]) ?? state.surfaceWater,
        groundwater: (d.groundwater as number[]) ?? state.groundwater,
        nutrient: (d.nutrient as number[]) ?? state.nutrient,
        permeability: (d.permeability as number[]) ?? state.permeability,
        pressure: (d.pressure as number[]) ?? state.pressure,
        windX: (d.wind_x as number[]) ?? state.windX,
        windY: (d.wind_y as number[]) ?? state.windY,
        sunlight: (d.sunlight as number[]) ?? state.sunlight,
        biome: (d.biome as number[]) ?? state.biome,
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
    heightMap: state.heightMap,
    slope: state.slope,
    riverFlow: state.riverFlow,
    surfaceWater: state.surfaceWater,
    groundwater: state.groundwater,
    nutrient: state.nutrient,
    permeability: state.permeability,
    pressure: state.pressure,
    windX: state.windX,
    windY: state.windY,
    sunlight: state.sunlight,
    biome: state.biome,
    waterCycle: state.waterCycle,
    plantCount: state.plantCount,
    stats: state.stats,
  };
}
