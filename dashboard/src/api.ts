import type { Stats, PaginatedResponse, VolumeHora } from './types';
import { demo } from './demo';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000';

/** `?demo` na URL troca a API real por dados sintéticos (só para revisar a UI). */
export const IS_DEMO =
  typeof window !== 'undefined' && new URLSearchParams(window.location.search).has('demo');

async function fetchJson<T>(path: string): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`);
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json();
}

export const api = {
  getStats: () => (IS_DEMO ? demo.stats() : fetchJson<Stats>('/api/dashboard/stats')),

  getTransactions: (page = 1, size = 15, status?: string) => {
    if (IS_DEMO) return demo.transactions(page, size, status);
    let url = `/api/dashboard/transactions?page=${page}&size=${size}`;
    if (status) url += `&status=${status}`;
    return fetchJson<PaginatedResponse>(url);
  },

  getTimeline: () => (IS_DEMO ? demo.timeline() : fetchJson<VolumeHora[]>('/api/dashboard/timeline')),

  getMlMetrics: () =>
    IS_DEMO
      ? demo.ml()
      : fetchJson<{
          modelLoaded: boolean;
          lastTrained: string | null;
          algorithm: string;
          features: string[];
        }>('/api/dashboard/ml/metrics'),
};
