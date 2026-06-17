import { useEffect, useState, useCallback } from 'react';
import type { StatsData } from '../types';

const API_URL = import.meta.env.DEV
  ? 'http://localhost:8088/api/stats'
  : '/api/stats';

export function useStats(refreshInterval = 5000) {
  const [stats, setStats] = useState<StatsData | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchStats = useCallback(async () => {
    try {
      const res = await fetch(API_URL);
      if (res.ok) {
        const data = await res.json();
        setStats(data);
      }
    } catch {
      // silently retry
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchStats();
    const id = setInterval(fetchStats, refreshInterval);
    return () => clearInterval(id);
  }, [fetchStats, refreshInterval]);

  return { stats, loading, refetch: fetchStats };
}
