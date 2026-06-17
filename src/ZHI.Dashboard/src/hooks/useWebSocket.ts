import { useEffect, useRef, useState, useCallback } from 'react';
import type { AgentSnapshot, FoodTile, LogMessage } from '../types';

const WS_URL = import.meta.env.DEV
  ? 'ws://localhost:8088/ws'
  : `${location.protocol === 'https:' ? 'wss:' : 'ws:'}//${location.host}/ws`;

export function useWebSocket() {
  const [connected, setConnected] = useState(false);
  const [generation, setGeneration] = useState(1);
  const [totalDeaths, setTotalDeaths] = useState(0);
  const [agents, setAgents] = useState<AgentSnapshot[]>([]);
  const [food, setFood] = useState<FoodTile[]>([]);
  const [logs, setLogs] = useState<LogMessage[]>([]);
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
          setAgents(data.agents ?? []);
          setFood(data.food ?? []);
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

  return { connected, generation, totalDeaths, agents, food, logs };
}
