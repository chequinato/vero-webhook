import type { Stats, PaginatedResponse, VolumeHora } from './types';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000';

async function fetchJson<T>(path: string): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`);
  if (!res.ok) throw new Error(`API error: ${res.status}`);
  return res.json();
}

export const api = {
  getStats: () => fetchJson<Stats>('/api/dashboard/stats'),

  getTransactions: (page = 1, size = 15, status?: string) => {
    let url = `/api/dashboard/transactions?page=${page}&size=${size}`;
    if (status) url += `&status=${status}`;
    return fetchJson<PaginatedResponse>(url);
  },

  getTimeline: () => fetchJson<VolumeHora[]>('/api/dashboard/timeline'),

  getMlMetrics: () => fetchJson<{
    modelLoaded: boolean;
    lastTrained: string | null;
    algorithm: string;
    features: string[];
  }>('/api/dashboard/ml/metrics'),
};
