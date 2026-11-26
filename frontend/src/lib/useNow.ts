import { useEffect, useState } from 'react';

/** Текущее время, которое само обновляется раз в 30 секунд: «идёт сейчас», «через 5 минут», прогресс брони. */
export function useNow(intervalMs = 30_000) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const t = window.setInterval(() => setNow(Date.now()), intervalMs);
    return () => window.clearInterval(t);
  }, [intervalMs]);
  return now;
}
