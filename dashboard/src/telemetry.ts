import { useSyncExternalStore, useMemo } from 'react';

/**
 * TELEMETRIA DO ENLACE
 *
 * Tudo aqui é medido, nada é simulado. Cada chamada à API passa por
 * `sonda()`, que cronometra a promessa e registra o desfecho; o hub e o
 * laço de animação alimentam os outros dois contadores. O painel de
 * observabilidade lê exclusivamente estes registros — se a API cair, os
 * números caem junto, que é o ponto de ter um painel de observabilidade.
 *
 * A janela é curta de propósito (algumas centenas de amostras): isto mede
 * a saúde do enlace navegador→API nesta sessão, não a do serviço inteiro.
 */

export interface Amostra {
  t: number;         // epoch ms
  rota: string;      // rótulo curto da rota
  ms: number;        // duração medida
  ok: boolean;
}

const TETO_AMOSTRAS = 480;
const TETO_EVENTOS = 600;
const TETO_QUADROS = 60;

const amostras: Amostra[] = [];
const eventos: number[] = [];      // carimbos de chegada vindos do hub
const quadros: number[] = [];      // fps por janela de 1s

let versao = 0;
const ouvintes = new Set<() => void>();

function emitir() {
  versao++;
  for (const fn of ouvintes) fn();
}

function inscrever(fn: () => void) {
  ouvintes.add(fn);
  return () => { ouvintes.delete(fn); };
}

/** Cronometra uma promessa e registra o desfecho. */
export async function sonda<T>(rota: string, exec: () => Promise<T>): Promise<T> {
  const t0 = performance.now();
  try {
    const r = await exec();
    registrar(rota, performance.now() - t0, true);
    return r;
  } catch (err) {
    registrar(rota, performance.now() - t0, false);
    throw err;
  }
}

export function registrar(rota: string, ms: number, ok: boolean) {
  amostras.push({ t: Date.now(), rota, ms, ok });
  if (amostras.length > TETO_AMOSTRAS) amostras.splice(0, amostras.length - TETO_AMOSTRAS);
  emitir();
}

/** Um evento chegou pelo hub — alimenta a vazão. */
export function registrarEvento() {
  eventos.push(Date.now());
  if (eventos.length > TETO_EVENTOS) eventos.splice(0, eventos.length - TETO_EVENTOS);
  emitir();
}

// ── Medidor de quadros: uma amostra por segundo, custo desprezível ──
if (typeof window !== 'undefined' && typeof requestAnimationFrame === 'function') {
  let conta = 0;
  let marco = performance.now();
  const passo = (agora: number) => {
    conta++;
    const decorrido = agora - marco;
    if (decorrido >= 1000) {
      // Aba oculta ou janela esticada por throttling do navegador não é
      // medida de desempenho — é ausência de medida. Descarta.
      const valida = decorrido < 2000 && document.visibilityState === 'visible';
      if (valida) {
        quadros.push(Math.round((conta * 1000) / decorrido));
        if (quadros.length > TETO_QUADROS) quadros.shift();
        emitir();
      }
      conta = 0;
      marco = agora;
    }
    requestAnimationFrame(passo);
  };
  requestAnimationFrame(passo);
}

// ══════════════════════════════════════════════════════════════════════
// DERIVAÇÃO
// ══════════════════════════════════════════════════════════════════════

function percentil(ordenado: number[], p: number): number {
  if (ordenado.length === 0) return 0;
  const i = Math.min(ordenado.length - 1, Math.max(0, Math.ceil((p / 100) * ordenado.length) - 1));
  return ordenado[i];
}

export type EstadoFatia = 'vazio' | 'ok' | 'lento' | 'falha';

export interface Rota {
  rota: string;
  n: number;
  p50: number;
  falhas: number;
}

export interface Snapshot {
  amostras: number;
  falhas: number;
  disponibilidade: number;    // %
  p50: number;
  p90: number;
  p99: number;
  pior: number;
  ultima: number;
  serie: number[];            // latências recentes, para o traço
  fatias: EstadoFatia[];      // fita de disponibilidade, 5s por fatia
  rotas: Rota[];
  vazao: number;              // eventos/min medidos na última janela
  eventosTotal: number;
  quadros: number | null;     // fps corrente; null enquanto não há medida
  quadrosSerie: number[];
  orcamento: number;          // % do orçamento de erro consumido
  saude: 'nominal' | 'lento' | 'degradado' | 'critico';
}

/** Acima disto a resposta conta como lenta na fita de disponibilidade. */
const LIMIAR_LENTO = 400;
/** Alvo de disponibilidade do enlace; define o orçamento de erro. */
const ALVO = 99.5;
const FATIAS = 48;
const JANELA_FATIA = 5000;

function derivar(): Snapshot {
  const agora = Date.now();
  const recentes = amostras.slice(-200);
  const durs = recentes.map(a => a.ms).sort((a, b) => a - b);
  const falhas = recentes.filter(a => !a.ok).length;
  const disponibilidade = recentes.length ? ((recentes.length - falhas) / recentes.length) * 100 : 100;

  // ── Fita: uma fatia por janela de 5s, da mais antiga à mais nova ──
  const fatias: EstadoFatia[] = [];
  for (let i = FATIAS - 1; i >= 0; i--) {
    const fim = agora - i * JANELA_FATIA;
    const ini = fim - JANELA_FATIA;
    const janela = amostras.filter(a => a.t > ini && a.t <= fim);
    if (janela.length === 0) fatias.push('vazio');
    else if (janela.some(a => !a.ok)) fatias.push('falha');
    else if (janela.some(a => a.ms > LIMIAR_LENTO)) fatias.push('lento');
    else fatias.push('ok');
  }

  // ── Quebra por rota ──
  const mapa = new Map<string, number[]>();
  const mapaFalhas = new Map<string, number>();
  for (const a of recentes) {
    if (!mapa.has(a.rota)) mapa.set(a.rota, []);
    mapa.get(a.rota)!.push(a.ms);
    if (!a.ok) mapaFalhas.set(a.rota, (mapaFalhas.get(a.rota) ?? 0) + 1);
  }
  const rotas: Rota[] = [...mapa.entries()]
    .map(([rota, ms]) => ({
      rota,
      n: ms.length,
      p50: percentil([...ms].sort((a, b) => a - b), 50),
      falhas: mapaFalhas.get(rota) ?? 0,
    }))
    .sort((a, b) => b.p50 - a.p50);

  // ── Vazão: eventos do hub no último minuto ──
  const janelaEventos = eventos.filter(t => agora - t < 60000);
  const consumo = ((100 - disponibilidade) / (100 - ALVO)) * 100;
  // Sem amostra não há número: a seção promete que nada é estimado.
  const fpsAtual = quadros.length ? quadros[quadros.length - 1] : null;

  const saude: Snapshot['saude'] =
    disponibilidade < 97 ? 'critico'
      : disponibilidade < ALVO ? 'degradado'
      : percentil(durs, 90) > LIMIAR_LENTO * 2 ? 'lento'
      : 'nominal';

  return {
    amostras: recentes.length,
    falhas,
    disponibilidade,
    p50: percentil(durs, 50),
    p90: percentil(durs, 90),
    p99: percentil(durs, 99),
    pior: durs.length ? durs[durs.length - 1] : 0,
    ultima: recentes.length ? recentes[recentes.length - 1].ms : 0,
    serie: recentes.slice(-56).map(a => a.ms),
    fatias,
    rotas,
    vazao: janelaEventos.length,
    eventosTotal: eventos.length,
    quadros: fpsAtual,
    quadrosSerie: quadros.slice(-40),
    orcamento: Math.max(0, Math.min(100, consumo)),
    saude,
  };
}

// O snapshot é memoizado por versão: `useSyncExternalStore` exige que duas
// leituras seguidas sem mudança devolvam o mesmo objeto.
let cacheVersao = -1;
let cache: Snapshot | null = null;

function ler(): Snapshot {
  if (cacheVersao !== versao || cache === null) {
    cache = derivar();
    cacheVersao = versao;
  }
  return cache;
}

export function useTelemetria(): Snapshot {
  const v = useSyncExternalStore(inscrever, () => versao, () => 0);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  return useMemo(() => ler(), [v]);
}
