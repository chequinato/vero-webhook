import { useEffect, useRef, useState } from 'react';

/**
 * Largura corrente de um elemento, para SVGs que precisam do próprio
 * viewBox em pixels reais (grade, passo entre colunas, corte de rótulos).
 */
export function useWidth(inicial = 640) {
  const ref = useRef<HTMLDivElement>(null);
  const [w, setW] = useState(inicial);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([entry]) => setW(entry.contentRect.width));
    ro.observe(el);
    setW(el.clientWidth || inicial);
    return () => ro.disconnect();
  }, [inicial]);

  return [ref, Math.max(120, w)] as const;
}
