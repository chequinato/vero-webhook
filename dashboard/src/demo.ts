import type { Stats, VolumeHora, PaginatedResponse, TransacaoDto } from './types';

/**
 * Dados de demonstração.
 *
 * Só entram em cena com `?demo` na URL — servem para revisar a interface sem
 * subir a API, o Postgres e o worker. Nenhum caminho normal passa por aqui.
 */

const TIPOS = ['pix', 'ted', 'boleto', 'cartao'];
const MOTIVOS = [
  'valor acima do limite',
  'destinatário recém-criado',
  'velocidade anômala',
  'geolocalização divergente',
  'padrão fora do perfil',
];

// Gerador determinístico: a mesma tela em toda recarga.
function rng(seed: number) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

const rand = rng(20260906);

const ROWS: TransacaoDto[] = Array.from({ length: 138 }, (_, i) => {
  const r = rand();
  const status =
    r > 0.93 ? 'bloqueada' : r > 0.84 ? 'suspeita' : r > 0.79 ? 'aceita_provisoria' : 'aprovada';
  const risco =
    status === 'bloqueada' ? 0.72 + rand() * 0.27
      : status === 'suspeita' ? 0.42 + rand() * 0.28
      : status === 'aceita_provisoria' ? 0.3 + rand() * 0.2
      : rand() * 0.35;
  const ts = new Date(Date.now() - i * 1000 * 60 * 9 - Math.floor(rand() * 400000));

  return {
    id: `tx_${(0x5f0000 + i * 7919).toString(16)}${Math.floor(rand() * 1e6).toString(16)}`,
    status,
    valor: Math.round((40 + rand() * (status === 'bloqueada' ? 48000 : 6400)) * 100) / 100,
    tipo: TIPOS[Math.floor(rand() * TIPOS.length)],
    moeda: 'BRL',
    motivo: status === 'aprovada' ? null : MOTIVOS[Math.floor(rand() * MOTIVOS.length)],
    riskScore: Math.round(risco * 1000) / 1000,
    timestamp: ts.toISOString(),
    remetenteId: 1000 + Math.floor(rand() * 400),
    destinatarioId: 1000 + Math.floor(rand() * 400),
    createdAt: ts.toISOString(),
  };
});

const conta = (s: string) => ROWS.filter(t => t.status === s).length;
const somaValor = ROWS.reduce((a, t) => a + t.valor, 0);

const STATS: Stats = {
  total: ROWS.length,
  aprovadas: conta('aprovada'),
  bloqueadas: conta('bloqueada'),
  suspeitas: conta('suspeita'),
  aceitasProvisoria: conta('aceita_provisoria'),
  valorTotal: Math.round(somaValor),
  valorMedio: Math.round((somaValor / ROWS.length) * 100) / 100,
  riskScoreMedio: ROWS.reduce((a, t) => a + (t.riskScore ?? 0), 0) / ROWS.length,
};

const TIMELINE: VolumeHora[] = Array.from({ length: 24 }, (_, i) => {
  const hora = new Date(Date.now() - (23 - i) * 3600 * 1000);
  // Curva diurna: vale de madrugada, pico no fim da tarde.
  const h = hora.getHours();
  const curva = 0.25 + 0.75 * Math.max(0, Math.sin(((h - 4) / 24) * Math.PI * 1.6));
  const quantidade = Math.round(18 + curva * 90 + rand() * 14);
  const bloqueadas = Math.round(quantidade * (0.02 + rand() * 0.07));
  const suspeitas = Math.round(quantidade * (0.03 + rand() * 0.09));
  return {
    hora: hora.toISOString(),
    quantidade,
    valor: Math.round(quantidade * (180 + rand() * 900)),
    bloqueadas,
    suspeitas,
  };
});

const espera = <T,>(v: T, ms = 260): Promise<T> =>
  new Promise(resolve => setTimeout(() => resolve(v), ms));

export const demo = {
  stats: () => espera(STATS),
  timeline: () => espera(TIMELINE),

  transactions: (page: number, size: number, status?: string): Promise<PaginatedResponse> => {
    const filtradas = status ? ROWS.filter(t => t.status.replace(/_/g, '') === status) : ROWS;
    const items = filtradas.slice((page - 1) * size, page * size);
    return espera({
      items,
      total: filtradas.length,
      page,
      size,
      totalPages: Math.max(1, Math.ceil(filtradas.length / size)),
    });
  },

  ml: () =>
    espera({
      modelLoaded: true,
      lastTrained: new Date(Date.now() - 3600 * 1000 * 5).toISOString(),
      algorithm: 'isolation-forest',
      features: ['valor', 'velocidade', 'hora', 'destino', 'desvio-perfil'],
    }),

  /** Um evento sintético para a fita ao vivo. */
  evento: () => {
    const base = ROWS[Math.floor(Math.random() * ROWS.length)];
    return { ...base, timestamp: new Date().toISOString() };
  },
};
