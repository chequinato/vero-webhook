import type { Stats, PaginatedResponse, VolumeHora, MlMetrics, Reavaliacao } from './types';
import { demo } from './demo';
import { sonda } from './telemetry';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000';

/** `?demo` na URL troca a API real por dados sintéticos (só para revisar a UI). */
export const IS_DEMO =
  typeof window !== 'undefined' && new URLSearchParams(window.location.search).has('demo');

async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, init);
  if (!res.ok) {
    // A API devolve `{ erro }` nos casos previstos (404, 409). Aproveitamos
    // a mensagem dela em vez de imprimir um código cru na tela do analista.
    const corpo = await res.json().catch(() => null);
    throw new Error(corpo?.erro ?? `API error: ${res.status}`);
  }
  return res.json();
}

/**
 * Toda chamada — real ou sintética — passa pela sonda. É dela que sai a
 * latência do painel de observabilidade, então nenhum caminho pode escapar.
 */
const chamar = <T>(rota: string, exec: () => Promise<T>) => sonda(rota, exec);

export const api = {
  getStats: () =>
    chamar('stats', () => (IS_DEMO ? demo.stats() : fetchJson<Stats>('/api/dashboard/stats'))),

  getTransactions: (page = 1, size = 15, status?: string) =>
    chamar('transactions', () => {
      if (IS_DEMO) return demo.transactions(page, size, status);
      let url = `/api/dashboard/transactions?page=${page}&size=${size}`;
      if (status) url += `&status=${status}`;
      return fetchJson<PaginatedResponse>(url);
    }),

  getTimeline: () =>
    chamar('timeline', () =>
      IS_DEMO ? demo.timeline() : fetchJson<VolumeHora[]>('/api/dashboard/timeline'),
    ),

  getMlMetrics: () =>
    chamar('ml/metrics', () =>
      IS_DEMO ? demo.ml() : fetchJson<MlMetrics>('/api/dashboard/ml/metrics'),
    ),

  /**
   * Amostra ampla usada pelas análises de composição — rosca, distribuição
   * de risco e matriz. A API limita `size` a 100, então este é o corte
   * máximo disponível numa chamada; as seções deixam isso explícito.
   */
  getSample: (size = 100) =>
    chamar('sample', () =>
      IS_DEMO
        ? demo.transactions(1, size)
        : fetchJson<PaginatedResponse>(`/api/dashboard/transactions?page=1&size=${size}`),
    ),

  /**
   * Pede ao modelo que resolva uma transação parada em revisão. Quem decide
   * é o modelo — o operador só dispara a avaliação.
   */
  reavaliar: (id: string) =>
    chamar('reavaliar', () =>
      IS_DEMO
        ? demo.reavaliar(id)
        : fetchJson<Reavaliacao>(
            `/api/dashboard/transactions/${encodeURIComponent(id)}/reavaliar`,
            { method: 'POST' },
          ),
    ),
};
