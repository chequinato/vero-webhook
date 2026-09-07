import { useState, useEffect } from 'react';

/**
 * Relógio UTC que atualiza a cada segundo.
 * Estilo telemetria: "19:42:07 UTC"
 */
export function useClock(): string {
  const [time, setTime] = useState(() => formatUTC());

  useEffect(() => {
    const interval = setInterval(() => setTime(formatUTC()), 1000);
    return () => clearInterval(interval);
  }, []);

  return time;
}

function formatUTC(): string {
  const now = new Date();
  const h = String(now.getUTCHours()).padStart(2, '0');
  const m = String(now.getUTCMinutes()).padStart(2, '0');
  const s = String(now.getUTCSeconds()).padStart(2, '0');
  return `${h}:${m}:${s} UTC`;
}
