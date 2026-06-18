import { useEffect, useRef, useState, useCallback } from 'react';
import type { LogMessage, WorldEvent } from '../types';

const WS_URL = import.meta.env.DEV
  ? 'ws://localhost:8088/ws/logs'
  : `${location.protocol === 'https:' ? 'wss:' : 'ws:'}//${location.host}/ws/logs`;

const MAX_EVENTS = 2000;

export function useLogSocket() {
  const [connected, setConnected] = useState(false);
  const [logs, setLogs] = useState<LogMessage[]>([]);
  const [events, setEvents] = useState<WorldEvent[]>([]);
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
        case 'log':
          setLogs(prev => {
            const next = [...prev, msg as LogMessage];
            return next.length > 500 ? next.slice(-500) : next;
          });
          break;
        case 'events': {
          const newEvents = msg.data as WorldEvent[];
          setEvents(prev => {
            const next = [...prev, ...newEvents];
            return next.length > MAX_EVENTS ? next.slice(-MAX_EVENTS) : next;
          });
          break;
        }
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

  return { connected: connected, logs, events, clearEvents };
}
