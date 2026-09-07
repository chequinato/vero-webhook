import type { Stats, VolumeHora, PaginatedResponse, TransacaoDto, Reavaliacao } from './types';

/**
 * Dados de demonstração — **vivos**.
 *
 * Só entram em cena com `?demo` na URL. A diferença para uma pilha de dados
 * estáticos é que este módulo guarda estado mutável: cada evento sintético
 * cria uma transação de verdade, empilha no livro, incrementa o balde da hora
 * corrente e passa a contar nas estatísticas. É por isso que o painel se mexe
 * em modo demonstração — os números derivam do mesmo lugar que derivariam da
 * API, e não de constantes congeladas na carga.
 *
 * Serve para revisar a interface sem subir API, Postgres e worker. Nenhum
 * caminho normal passa por aqui.
 */

const TIPOS = ['pix', 'ted', 'boleto', 'cartao'];
const MOTIVOS = [
  'valor acima do limite',
  'destinatário recém-criado',
  'velocidade anômala',
  'geolocalização divergente',
  'padrão fora do perfil',
];

/** Corte de bloqueio do modelo — o mesmo do `DashboardController`. */
export const LIMIAR_BLOQUEIO = 0.7;

/** Teto do livro em memória; acima disso a cauda antiga é descartada. */
const TETO_LINHAS = 800;

// Gerador determinístico para o lastro inicial: a mesma tela em toda recarga.
// O que vem depois, ao vivo, usa Math.random — senão a sessão seria um loop.
function rng(seed: number) {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 4294967296;
  };
}

const rand = rng(20260906);

function sorteiaStatus(r: number): string {
  return r > 0.93 ? 'bloqueada'
    : r > 0.84 ? 'suspeita'
    : r > 0.79 ? 'aceita_provisoria'
    : 'aprovada';
}

function scoreDe(status: string, sorteio: () => number): number {
  const risco =
    status === 'bloqueada' ? 0.72 + sorteio() * 0.27
      : status === 'suspeita' ? 0.42 + sorteio() * 0.28
      : status === 'aceita_provisoria' ? 0.3 + sorteio() * 0.2
      : sorteio() * 0.35;
  return Math.round(risco * 1000) / 1000;
}

// ── Lastro: 138 linhas plausíveis, do mais recente para o mais antigo ──
const ROWS: TransacaoDto[] = Array.from({ length: 138 }, (_, i) => {
  const status = sorteiaStatus(rand());
  const ts = new Date(Date.now() - i * 1000 * 60 * 9 - Math.floor(rand() * 400000));

  return {
    id: `tx_${(0x5f0000 + i * 7919).toString(16)}${Math.floor(rand() * 1e6).toString(16)}`,
    status,
    valor: Math.round((40 + rand() * (status === 'bloqueada' ? 48000 : 6400)) * 100) / 100,
    tipo: TIPOS[Math.floor(rand() * TIPOS.length)],
    moeda: 'BRL',
    motivo: status === 'aprovada' ? null : MOTIVOS[Math.floor(rand() * MOTIVOS.length)],
    riskScore: scoreDe(status, rand),
    timestamp: ts.toISOString(),
    remetenteId: 1000 + Math.floor(rand() * 400),
    destinatarioId: 1000 + Math.floor(rand() * 400),
    createdAt: ts.toISOString(),
  };
});

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

// ══════════════════════════════════════════════════════════════════════
// DERIVAÇÃO — recalculada a cada leitura, como faria a API
// ══════════════════════════════════════════════════════════════════════

function conta(s: string) {
  return ROWS.filter(t => t.status === s).length;
}

function estatisticas(): Stats {
  const soma = ROWS.reduce((a, t) => a + t.valor, 0);
  return {
    total: ROWS.length,
    aprovadas: conta('aprovada'),
    bloqueadas: conta('bloqueada'),
    suspeitas: conta('suspeita'),
    aceitasProvisoria: conta('aceita_provisoria'),
    valorTotal: Math.round(soma),
    valorMedio: Math.round((soma / Math.max(1, ROWS.length)) * 100) / 100,
    riskScoreMedio: ROWS.reduce((a, t) => a + (t.riskScore ?? 0), 0) / Math.max(1, ROWS.length),
  };
}

/** Empilha uma linha nova e reflete no balde da hora corrente. */
function registrar(tx: TransacaoDto) {
  ROWS.unshift(tx);
  if (ROWS.length > TETO_LINHAS) ROWS.length = TETO_LINHAS;

  const balde = TIMELINE[TIMELINE.length - 1];
  balde.quantidade += 1;
  balde.valor += Math.round(tx.valor);
  if (tx.status === 'bloqueada') balde.bloqueadas += 1;
  if (tx.status === 'suspeita') balde.suspeitas += 1;
}

/**
 * Latência sintética. Curta de propósito: o painel de observabilidade mede
 * isto de verdade, então um número inflado aqui mentiria sobre o enlace.
 */
const espera = <T,>(v: T, ms = 120): Promise<T> =>
  new Promise(resolve => setTimeout(() => resolve(v), ms + Math.random() * 60));

export const demo = {
  stats: () => espera(estatisticas()),

  timeline: () => espera(TIMELINE.map(v => ({ ...v }))),

  transactions: (page: number, size: number, status?: string): Promise<PaginatedResponse> => {
    const filtradas = status ? ROWS.filter(t => t.status.replace(/_/g, '') === status) : ROWS;
    const items = filtradas.slice((page - 1) * size, page * size).map(t => ({ ...t }));
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

  /** Uma transação nova, de verdade: entra no livro e conta nos totais. */
  evento: (): TransacaoDto => {
    const r = Math.random();
    const status = sorteiaStatus(r);
    const agora = new Date();

    const tx: TransacaoDto = {
      id: `tx_${Math.floor(Math.random() * 0xffffff).toString(16).padStart(6, '0')}${Date.now()
        .toString(16)
        .slice(-5)}`,
      status,
      valor: Math.round((40 + Math.random() * (status === 'bloqueada' ? 48000 : 6400)) * 100) / 100,
      tipo: TIPOS[Math.floor(Math.random() * TIPOS.length)],
      moeda: 'BRL',
      motivo: status === 'aprovada' ? null : MOTIVOS[Math.floor(Math.random() * MOTIVOS.length)],
      riskScore: scoreDe(status, Math.random),
      timestamp: agora.toISOString(),
      remetenteId: 1000 + Math.floor(Math.random() * 400),
      destinatarioId: 1000 + Math.floor(Math.random() * 400),
      createdAt: agora.toISOString(),
    };

    registrar(tx);
    return tx;
  },

  /**
   * Reavaliação pelo "modelo". Espelha o endpoint real: o score é
   * recalculado — não reaproveitado — porque as features de velocidade
   * mudaram desde a entrada. Por isso uma suspeita de 0,60 pode perfeitamente
   * cruzar o corte e virar bloqueio; se apenas reutilizássemos o score
   * antigo, o botão sempre aprovaria e a feature seria teatro.
   */
  reavaliar: (id: string): Promise<Reavaliacao> => {
    const tx = ROWS.find(t => t.id === id);
    if (!tx) return Promise.reject(new Error('Transação não encontrada.'));
    if (tx.status !== 'suspeita' && tx.status !== 'aceita_provisoria') {
      return Promise.reject(new Error('Só transações em revisão podem ser reavaliadas.'));
    }

    const deriva = (Math.random() - 0.42) * 0.5;
    const score = Math.round(Math.min(0.99, Math.max(0.01, (tx.riskScore ?? 0.5) + deriva)) * 1000) / 1000;
    const bloqueia = score >= LIMIAR_BLOQUEIO;

    tx.status = bloqueia ? 'bloqueada' : 'aprovada';
    tx.motivo = bloqueia ? 'modelo: risco acima do corte' : null;
    tx.riskScore = score;

    return espera(
      {
        id,
        status: tx.status,
        motivo: tx.motivo,
        riskScore: score,
        limiar: LIMIAR_BLOQUEIO,
      },
      420,
    );
  },
};
