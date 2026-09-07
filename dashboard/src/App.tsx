import { useState, useEffect, useCallback, useRef, lazy, Suspense } from 'react';
import { api, IS_DEMO } from './api';
import { useSignalR } from './useSignalR';
import { useClock } from './hooks/useClock';
import { useTelemetria } from './telemetry';
import {
  somaTransacao,
  trocaStatus,
  somaNaHora,
  insereNoTopo,
  aplicaPatch,
  casaFiltro,
} from './live';
import { lancar } from './engine/bus';
import { Mark } from './components/Mark';
import { Situation } from './components/Situation';
import { VolumeChart } from './components/VolumeChart';
import { Feed } from './components/Feed';
import { Composition } from './components/Composition';
import { Observability } from './components/Observability';
import { Ledger } from './components/Ledger';
import type { Stats, VolumeHora, TransacaoDto, MlMetrics, StatusPatch } from './types';
import './App.css';

/**
 * A cena 3D carrega o three.js inteiro (~600 kB). Ela é o palco, mas não pode
 * atrasar a primeira pintura da folha: entra em pedaço separado, e o resto do
 * boletim já está legível enquanto ela chega.
 */
const Engine = lazy(() => import('./components/Engine').then(m => ({ default: m.Engine })));

type Mode = 'paper' | 'ink';

/** Linhas por página do livro. */
const PAGINA = 15;
/** Tamanho da amostra que alimenta anel, distribuição e matriz. */
const AMOSTRA = 100;
/** Silêncio necessário depois do último evento para reconciliar. */
const QUIETUDE = 2500;

function useMode() {
  const [mode, setMode] = useState<Mode>(
    () => (document.documentElement.dataset.mode as Mode) || 'paper',
  );

  useEffect(() => {
    document.documentElement.dataset.mode = mode;
    try {
      localStorage.setItem('vero.mode', mode);
    } catch {
      /* armazenamento bloqueado — a preferência vale só para esta sessão */
    }
  }, [mode]);

  return [mode, () => setMode(m => (m === 'paper' ? 'ink' : 'paper'))] as const;
}

function App() {
  const [stats, setStats] = useState<Stats | null>(null);
  const [timeline, setTimeline] = useState<VolumeHora[]>([]);
  const [transactions, setTransactions] = useState<TransacaoDto[]>([]);
  const [amostra, setAmostra] = useState<TransacaoDto[]>([]);
  const [modelo, setModelo] = useState<MlMetrics | null>(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filter, setFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [sync, setSync] = useState<Date | null>(null);
  const [aoVivo, setAoVivo] = useState(0);
  const [refreshKey, setRefreshKey] = useState(0);

  const clock = useClock();
  const [, toggleMode] = useMode();
  const tel = useTelemetria();

  // Último estado conhecido de cada linha, para saber de qual coluna
  // descontar quando o hub avisa que uma transação mudou de status.
  const conhecidas = useRef(new Map<string, { status: string; riskScore: number | null }>());
  const reconRef = useRef(0);

  const memorizar = useCallback((linhas: TransacaoDto[]) => {
    for (const t of linhas) conhecidas.current.set(t.id, { status: t.status, riskScore: t.riskScore });
    // O mapa só existe para resolver patches recentes; não vale reter a
    // sessão inteira dentro dele.
    if (conhecidas.current.size > 1200) {
      const sobra = [...conhecidas.current.keys()].slice(0, conhecidas.current.size - 800);
      for (const k of sobra) conhecidas.current.delete(k);
    }
  }, []);

  const fetchData = useCallback(async () => {
    try {
      const [statsData, timelineData, txData, amostraData] = await Promise.all([
        api.getStats(),
        api.getTimeline(),
        api.getTransactions(page, PAGINA, filter || undefined),
        api.getSample(AMOSTRA),
      ]);
      setStats(statsData);
      setTimeline(timelineData);
      setTransactions(txData.items);
      setTotal(txData.total);
      setTotalPages(txData.totalPages);
      setAmostra(amostraData.items);
      memorizar(amostraData.items);
      memorizar(txData.items);
      setSync(new Date());
    } catch (err) {
      console.warn('API não disponível:', err);
    } finally {
      setLoading(false);
    }
  }, [page, filter, memorizar]);

  useEffect(() => {
    fetchData();
  }, [fetchData, refreshKey]);

  useEffect(() => {
    api.getMlMetrics().then(setModelo).catch(() => setModelo(null));
  }, []);

  // Piso de reconciliação: mesmo num fluxo intenso, o servidor tem a palavra
  // final a cada dez segundos.
  useEffect(() => {
    const t = setInterval(() => setRefreshKey(k => k + 1), 10000);
    return () => clearInterval(t);
  }, []);

  useEffect(() => () => clearTimeout(reconRef.current), []);

  /**
   * Reconciliação preguiçosa: só busca quando o barramento fica quieto. Numa
   * rajada, buscar a cada evento derrubaria a API e faria a tela piscar sem
   * necessidade — os agregados locais já estão certos no intervalo.
   */
  const agendarReconciliacao = useCallback((atraso = QUIETUDE) => {
    clearTimeout(reconRef.current);
    reconRef.current = window.setTimeout(() => setRefreshKey(k => k + 1), atraso);
  }, []);

  // ── Transação nova: tudo se move agora, o servidor confirma depois ──
  const aoChegar = useCallback(
    (tx: TransacaoDto) => {
      conhecidas.current.set(tx.id, { status: tx.status, riskScore: tx.riskScore });

      // A cena 3D recebe o evento pelo barramento próprio, sem passar pelo
      // ciclo de render — ver `engine/bus.ts`.
      lancar(tx);

      setStats(s => (s ? somaTransacao(s, tx) : s));
      setTimeline(t => somaNaHora(t, tx));
      setAmostra(a => insereNoTopo(a, tx, AMOSTRA));
      setTotal(n => n + 1);
      setAoVivo(n => n + 1);

      // A primeira página é uma janela sobre o topo do livro; páginas
      // internas ficam paradas de propósito, senão a linha que o operador
      // está lendo escorregaria para baixo enquanto ele lê.
      if (page === 1 && casaFiltro(tx, filter)) {
        setTransactions(list => insereNoTopo(list, tx, PAGINA));
      }

      agendarReconciliacao();
    },
    [page, filter, agendarReconciliacao],
  );

  // ── Mudança de status (reavaliação, worker, regra assíncrona) ──
  const aoMudarStatus = useCallback(
    (p: StatusPatch) => {
      const antes = conhecidas.current.get(p.id);

      // O mesmo desfecho chega duas vezes: uma na resposta do POST, outra no
      // eco do hub. Aplicar é idempotente, mas contar não seria — e um
      // segundo `refreshKey` logo atrás do primeiro é busca jogada fora.
      if (antes && antes.status === p.status && antes.riskScore === p.riskScore) return;

      conhecidas.current.set(p.id, { status: p.status, riskScore: p.riskScore });

      const campos: Partial<TransacaoDto> = {
        status: p.status,
        motivo: p.motivo,
        ...(p.riskScore !== null && p.riskScore !== undefined ? { riskScore: p.riskScore } : {}),
      };

      setTransactions(list => aplicaPatch(list, p.id, campos));
      setAmostra(list => aplicaPatch(list, p.id, campos));

      if (antes) {
        setStats(s => (s ? trocaStatus(s, antes.status, p.status, antes.riskScore, p.riskScore) : s));
      }

      setAoVivo(n => n + 1);
      // Mudança de status mexe nos baldes da hora, que não dá para corrigir
      // sem o carimbo original — o servidor resolve isso logo em seguida.
      agendarReconciliacao(900);
    },
    [agendarReconciliacao],
  );

  const { connected, alerts } = useSignalR({ onTransacao: aoChegar, onStatus: aoMudarStatus });

  const handleFilterChange = useCallback((f: string) => {
    setFilter(f);
    setPage(1);
  }, []);

  /** Clicar num portão do motor leva o registro junto e desce até ele. */
  const filtrarPeloMotor = useCallback(
    (status: string) => {
      handleFilterChange(status);
      document.querySelector('.log-head')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    },
    [handleFilterChange],
  );

  /** Botão de reavaliação do livro: quem decide é o modelo. */
  const reavaliar = useCallback(
    async (id: string) => {
      const r = await api.reavaliar(id);
      // Em modo demonstração não há hub para devolver o eco, e mesmo com a
      // API real a resposta chega antes da mensagem do SignalR. Aplicar aqui
      // deixa a linha resolvida no instante do clique; o eco, quando vier,
      // é idempotente.
      aoMudarStatus({ id: r.id, status: r.status, motivo: r.motivo, riskScore: r.riskScore });
      return r;
    },
    [aoMudarStatus],
  );

  const hoje = new Date().toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });

  // Chave da varredura: remonta o fio a cada sincronia, e ele passa uma vez.
  const varredura = sync ? sync.getTime() : 0;

  return (
    <div className="frame">
      {/* ══ TRILHO ══ */}
      <aside className="rail">
        <span className="rail-mark">
          <Mark size={26} />
        </span>
        <span className="rail-word">Vero</span>

        <span className="rail-spacer" />

        {/* Coluna de saúde: o estado do enlace impresso na lombada. */}
        <span className={`rail-saude rail-saude--${tel.saude}`} title={`enlace ${tel.saude}`}>
          {tel.disponibilidade.toFixed(0)}
        </span>

        <span className="rail-seg rv-vrule" style={{ ['--i' as string]: 6 }} />

        <span className="rail-state">
          <span className={`led${connected ? ' led--on' : ''}`} />
          <span className="rail-state-word">{connected ? 'enlace' : 'mudo'}</span>
        </span>

        <button
          className="invert"
          onClick={toggleMode}
          title="Inverter papel e tinta"
          aria-label="Inverter papel e tinta"
        />
      </aside>

      {/* ══ FOLHA ══ */}
      <div className="sheet">
        <header className="masthead">
          <h1 className="masthead-title rv" style={{ ['--i' as string]: 0 }}>
            Boletim de integridade
            <span className="masthead-sub">transacional</span>
          </h1>
          <div className="masthead-meta rv" style={{ ['--i' as string]: 1 }}>
            <span>ed. {hoje}</span>
            <span>
              <b>{clock}</b>
            </span>
            <span>
              enlace <b>{connected ? 'ativo' : 'caído'}</b>
            </span>
          </div>
        </header>

        {/* ══ 00 — MOTOR (o palco) ══ */}
        <Suspense
          fallback={
            <section className="motor">
              <div className="motor-head">
                <span>
                  <span className="idx">00</span>{' '}
                  <span className="name">Motor de decisão — ao vivo</span>
                </span>
              </div>
              <div className="motor-palco motor-palco--espera">montando o motor</div>
            </section>
          }
        >
          <Engine amostra={amostra} conectado={connected} onFiltrar={filtrarPeloMotor} />
        </Suspense>

        <div className="section-mark rv" style={{ ['--i' as string]: 1 }}>
          <span className="idx">01</span>
          <span className="name">Situação — janela de 24 horas</span>
        </div>

        <Situation stats={stats} loading={loading} />

        {/* ── fita de sistema ── */}
        <div className="systembar rv" style={{ ['--i' as string]: 7 }}>
          <span>
            modelo <b>{modelo?.algorithm ?? 'indisponível'}</b>
          </span>
          <span>
            estado <b>{modelo?.modelLoaded ? 'carregado' : 'não carregado'}</b>
          </span>
          <span>
            varredura <b>10s</b>
          </span>
          <span>
            ao vivo <b>{String(aoVivo).padStart(3, '0')}</b>
          </span>
          <span>
            eventos <b>{String(alerts.length).padStart(3, '0')}</b>
          </span>
          <span>
            latência <b>{Math.round(tel.p50)}ms</b>
          </span>
          <span>
            sincronia{' '}
            <b>{sync ? sync.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) : '——'}</b>
          </span>
          {IS_DEMO && (
            <span>
              fonte <b>demonstração</b>
            </span>
          )}
        </div>

        {/* ══ FAIXA DE TELEMETRIA — fluxo e feed ══ */}
        <section className="band rv" style={{ ['--i' as string]: 8 }}>
          <span className="band-sweep" key={varredura} aria-hidden="true" />

          <div className="band-grid">
            <div className="band-panel">
              <div className="band-head">
                <span>
                  <span className="idx">02</span> <span className="name">Fluxo por hora</span>
                </span>
                <span className="band-legend">
                  <span>
                    <i style={{ background: 'var(--zone-fg)', opacity: 0.78 }} />
                    normal
                  </span>
                  <span>
                    <i style={{ background: 'var(--zone-amber)' }} />
                    suspeita
                  </span>
                  <span>
                    <i style={{ background: 'var(--zone-signal)' }} />
                    bloqueio
                  </span>
                  <span>
                    <i style={{ background: 'var(--zone-fg)', height: 1, width: 14 }} />
                    taxa
                  </span>
                </span>
              </div>
              <VolumeChart data={timeline} />
            </div>

            <div className="band-panel">
              <div className="band-head">
                <span>
                  <span className="idx">03</span> <span className="name">Feed ao vivo</span>
                </span>
                <span className="band-legend">
                  <span>
                    <i
                      style={{
                        background: connected ? 'var(--zone-verde)' : 'var(--zone-signal)',
                      }}
                    />
                    {connected ? 'recebendo' : 'sem sinal'}
                  </span>
                </span>
              </div>
              <Feed alerts={alerts} />
            </div>
          </div>
        </section>

        {/* ══ 04 — COMPOSIÇÃO ══ */}
        <Composition stats={stats} amostra={amostra} />

        {/* ══ FAIXA DE TELEMETRIA — observabilidade ══ */}
        <section className="band band--obs">
          <div className="band-head band-head--solo">
            <span>
              <span className="idx">05</span> <span className="name">Observabilidade do enlace</span>
            </span>
            <span className="band-legend">
              <span>medido no navegador · nada estimado</span>
            </span>
          </div>
          <Observability conectado={connected} />
        </section>

        <Ledger
          transactions={transactions}
          total={total}
          page={page}
          totalPages={totalPages}
          filter={filter}
          onFilterChange={handleFilterChange}
          onPageChange={setPage}
          onReavaliar={reavaliar}
        />

        <footer className="colophon">
          <span>Vero — detecção híbrida de anomalias</span>
          <span>SF Pro · Geist Mono · Newsreader</span>
          <span>{connected ? 'transmissão ao vivo' : 'leitura estática'}</span>
        </footer>
      </div>
    </div>
  );
}

export default App;
