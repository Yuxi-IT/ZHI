import { useEffect, useRef, useState, useCallback } from 'react';
import type { AgentSnapshot, FoodTile, CorpseTile, LogMessage, WorldEvent, CosmosStats } from '../types';

const WS_URL = import.meta.env.DEV
  ? 'ws://localhost:8088/ws'
  : `${location.protocol === 'https:' ? 'wss:' : 'ws:'}//${location.host}/ws`;

const MAX_EVENTS = 2000;

export function useWebSocket() {
  const [connected, setConnected] = useState(false);
  const [generation, setGeneration] = useState(1);
  const [totalDeaths, setTotalDeaths] = useState(0);
  const [timeOfDay, setTimeOfDay] = useState(0);
  const [temperature, setTemperature] = useState(20);
  const [gridW, setGridW] = useState(64);
  const [gridH, setGridH] = useState(64);
  const [agents, setAgents] = useState<AgentSnapshot[]>([]);
  const [food, setFood] = useState<FoodTile[]>([]);
  const [corpses, setCorpses] = useState<CorpseTile[]>([]);
  const [river, setRiver] = useState<number[]>([]);
  const [scent, setScent] = useState<number[]>([]);
  const [foodScent, setFoodScent] = useState<number[]>([]);
  const [signalField, setSignalField] = useState<number[]>([]);
  const [logs, setLogs] = useState<LogMessage[]>([]);
  const [events, setEvents] = useState<WorldEvent[]>([]);
  const [stats, setStats] = useState<CosmosStats | null>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const reconnectRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  const connect = useCallback(() => {
    if (wsRef.current?.readyState === WebSocket.OPEN) return;

    const ws = new WebSocket(WS_URL);
    wsRef.current = ws;

    ws.onopen = () => setConnected(true);

    ws.onclose = () => {
      setConnected(false);
      reconnectRef.current = setTimeout(connect, 2000);
    };

    ws.onerror = () => ws.close();

    ws.onmessage = (e) => {
      const msg = JSON.parse(e.data);
      switch (msg.type) {
        case 'cosmos': {
          const data = msg.data;
          setGeneration(data.generation);
          setTotalDeaths(data.total_deaths);
          if (data.time_of_day !== undefined) setTimeOfDay(data.time_of_day);
          if (data.temperature !== undefined) setTemperature(data.temperature);
          if (data.grid_width) setGridW(data.grid_width);
          if (data.grid_height) setGridH(data.grid_height);
          setAgents(data.agents ?? []);
          setFood(data.food ?? []);
          setCorpses(data.corpses ?? []);
          if (data.river) setRiver(data.river);
          if (data.scent) setScent(data.scent);
          if (data.food_scent) setFoodScent(data.food_scent);
          if (data.signal_field) setSignalField(data.signal_field);
          if (data.stats) setStats(data.stats);
          break;
        }
        case 'events': {
          const newEvents = msg.data as WorldEvent[];
          setEvents(prev => {
            const next = [...prev, ...newEvents];
            return next.length > MAX_EVENTS ? next.slice(-MAX_EVENTS) : next;
          });
          break;
        }
        case 'log':
          setLogs(prev => {
            const next = [...prev, msg as LogMessage];
            return next.length > 500 ? next.slice(-500) : next;
          });
          break;
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

  const clearEvents = useCallback(() => setEvents([]), []);

  return { connected, generation, totalDeaths, timeOfDay, temperature, gridW, gridH, agents, food, corpses, river, scent, foodScent, signalField, logs, events, clearEvents, stats };
}
