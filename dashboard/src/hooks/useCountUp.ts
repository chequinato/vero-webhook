import { useState, useEffect, useRef } from 'react';

/**
 * Anima um número de 0 até o valor alvo (ou do valor anterior ao novo).
 * Usa ease-out cúbico para desaceleração natural.
 */
export function useCountUp(target: number, duration = 900): number {
  const [display, setDisplay] = useState(0);
  const prevTarget = useRef(0);
  const frameRef = useRef(0);

  useEffect(() => {
    const from = prevTarget.current;
    const diff = target - from;
    if (Math.abs(diff) < 0.5) {
      setDisplay(target);
      prevTarget.current = target;
      return;
    }

    const startTime = performance.now();

    const tick = (now: number) => {
      const elapsed = now - startTime;
      const progress = Math.min(elapsed / duration, 1);
      // ease-out cubic
      const eased = 1 - Math.pow(1 - progress, 3);
      const current = from + diff * eased;
      setDisplay(Number.isInteger(target) ? Math.round(current) : current);

      if (progress < 1) {
        frameRef.current = requestAnimationFrame(tick);
      } else {
        setDisplay(target);
        prevTarget.current = target;
      }
    };

    frameRef.current = requestAnimationFrame(tick);

    return () => cancelAnimationFrame(frameRef.current);
  }, [target, duration]);

  return display;
}
