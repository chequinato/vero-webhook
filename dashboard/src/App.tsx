import { useState, useEffect, useCallback } from 'react';
import { api, IS_DEMO } from './api';
import { useSignalR } from './useSignalR';
import { useClock } from './hooks/useClock';
import { Mark } from './components/Mark';
import { Situation } from './components/Situation';
import { VolumeChart } from './components/VolumeChart';
import { Feed } from './components/Feed';
import { Ledger } from './components/Ledger';
import type { Stats, VolumeHora, TransacaoDto } from './types';
import './App.css';

type Mode = 'paper' | 'ink';

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
  const [modelo, setModelo] = useState<{ algorithm: string; modelLoaded: boolean } | null>(null);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filter, setFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [sync, setSync] = useState<Date | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  const clock = useClock();
  const [, toggleMode] = useMode();

  const fetchData = useCallback(async () => {
    try {
      const [statsData, timelineData, txData] = await Promise.all([
        api.getStats(),
        api.getTimeline(),
        api.getTransactions(page, 15, filter || undefined),
      ]);
      setStats(statsData);
      setTimeline(timelineData);
      setTransactions(txData.items);
      setTotal(txData.total);
      setTotalPages(txData.totalPages);
      setSync(new Date());
    } catch (err) {
      console.warn('API não disponível:', err);
    } finally {
      setLoading(false);
    }
  }, [page, filter]);

  useEffect(() => {
    fetchData();
  }, [fetchData, refreshKey]);

  useEffect(() => {
    api.getMlMetrics().then(setModelo).catch(() => setModelo(null));
  }, []);

  useEffect(() => {
    const t = setInterval(() => setRefreshKey(k => k + 1), 10000);
    return () => clearInterval(t);
  }, []);

  const handleNewTransaction = useCallback(() => setRefreshKey(k => k + 1), []);
  const { connected, alerts } = useSignalR(handleNewTransaction);

  const handleFilterChange = (f: string) => {
    setFilter(f);
    setPage(1);
  };

  const hoje = new Date().toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  });

  return (
    <div className="frame">
      {/* ══ TRILHO ══ */}
      <aside className="rail">
        <span className="rail-mark">
          <Mark size={26} />
        </span>
        <span className="rail-word">Vero</span>

        <span className="rail-spacer" />

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
            eventos <b>{String(alerts.length).padStart(3, '0')}</b>
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

        {/* ══ FAIXA DE TELEMETRIA ══ */}
        <section className="band rv" style={{ ['--i' as string]: 8 }}>
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

        <Ledger
          transactions={transactions}
          total={total}
          page={page}
          totalPages={totalPages}
          filter={filter}
          onFilterChange={handleFilterChange}
          onPageChange={setPage}
        />

        <footer className="colophon">
          <span>Vero — detecção híbrida de anomalias</span>
          <span>Archivo · IBM Plex Mono · Newsreader</span>
          <span>{connected ? 'transmissão ao vivo' : 'leitura estática'}</span>
        </footer>
      </div>
    </div>
  );
}

export default App;
