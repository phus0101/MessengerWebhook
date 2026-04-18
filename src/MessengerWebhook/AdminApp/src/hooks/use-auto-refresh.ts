import { useState, useEffect } from "react";

export function useAutoRefresh(enabled: boolean, intervalMs: number = 30000) {
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    if (!enabled) return;

    const interval = setInterval(() => {
      setRefreshKey(prev => prev + 1);
    }, intervalMs);

    return () => clearInterval(interval);
  }, [enabled, intervalMs]);

  return refreshKey;
}
