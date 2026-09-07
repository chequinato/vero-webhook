import { useState, useEffect, useCallback } from 'react';
import { api } from './api';
import { useSignalR } from './useSignalR';
import { useClock } from './hooks/useClock';
import { StatsCards } from './components/StatsCards';
import { VolumeChart } from './components/VolumeChart';
import { AlertFeed } from './components/AlertFeed';
import { TransactionTable } from './components/TransactionTable';
import type { Stats, VolumeHora, TransacaoDto } from './types';
import './App.css';

function App() {
  // ─── State ───
  const [stats, setStats] = useState<Stats | null>(null);
  const [timeline, setTimeline] = useState<VolumeHora[]>([]);
  const [transactions, setTransactions] = useState<TransacaoDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [filter, setFilter] = useState('');
  const [loading, setLoading] = useState(true);
  const [refreshKey, setRefreshKey] = useState(0);

  const clock = useClock();

  // ─── Data Fetching ───
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
    const interval = setInterval(() => {
      setRefreshKey(k => k + 1);
    }, 10000);
    return () => clearInterval(interval);
  }, []);

  // ─── SignalR ───
  const handleNewTransaction = useCallback(() => {
    setRefreshKey(k => k + 1);
  }, []);

  const { connected, alerts } = useSignalR(handleNewTransaction);

  // ─── Filter / Pagination ───
  const handleFilterChange = (f: string) => {
    setFilter(f);
    setPage(1);
  };

  return (
    <div className="app">
      {/* ── Header ── */}
      <header className="header">
        <div className="header-left">
          <div className="header-logo">
            <svg className="header-logo-mark" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              {/* Shield outline */}
              <path d="M16 2L4 8V16C4 22.6 9.2 28.4 16 30C22.8 28.4 28 22.6 28 16V8L16 2Z" stroke="var(--accent)" strokeWidth="1.2" opacity="0.15" fill="none"/>
              {/* V mark */}
              <path d="M9 9L16 24L23 9" stroke="var(--accent)" strokeWidth="2.8" strokeLinecap="square" strokeLinejoin="miter"/>
              {/* Scan line */}
              <line x1="11.5" y1="15" x2="20.5" y2="15" stroke="var(--accent)" strokeWidth="1" opacity="0.4"/>
              {/* Corner accents */}
              <line x1="4" y1="8" x2="7" y2="8" stroke="var(--accent)" strokeWidth="0.8" opacity="0.25"/>
              <line x1="25" y1="8" x2="28" y2="8" stroke="var(--accent)" strokeWidth="0.8" opacity="0.25"/>
            </svg>
            <span className="header-logo-text">VERO</span>
          </div>
          <div className="header-divider" />
          <span className="header-subtitle">Fraud Detection System</span>
        </div>
        <div className="header-right">
          <span className="header-clock">{clock}</span>
          <div className="connection-status">
            <span className={`connection-indicator ${connected ? 'connected' : ''}`} />
            <span>{connected ? 'SYS ONLINE' : 'SYS OFFLINE'}</span>
          </div>
        </div>
      </header>

      {/* ── Main ── */}
      <main className="main">
        {/* 01 — OVERVIEW */}
        <div className="section stagger-1">
          <div className="section-header">
            <span className="section-number">01</span>
            <span className="section-title">Overview</span>
          </div>
          <StatsCards stats={stats} loading={loading} />
        </div>

        {/* 02 & 03 — VOLUME + FEED */}
        <div className="section stagger-3">
          <div className="charts-row">
            <div className="chart-panel">
              <div className="chart-panel-header">
                <div>
                  <div style={{ display: 'flex', alignItems: 'baseline', gap: 12, marginBottom: 4 }}>
                    <span className="section-number">02</span>
                    <span className="chart-panel-title">Volume 24h</span>
                  </div>
                </div>
                <div className="chart-legend">
                  <div className="chart-legend-item">
                    <span className="chart-legend-dot" style={{ background: 'var(--accent)' }} />
                    Normal
                  </div>
                  <div className="chart-legend-item">
                    <span className="chart-legend-dot" style={{ background: 'var(--danger)' }} />
                    Bloqueada
                  </div>
                  <div className="chart-legend-item">
                    <span className="chart-legend-dot" style={{ background: 'var(--warning)' }} />
                    Suspeita
                  </div>
                </div>
              </div>
              <VolumeChart data={timeline} />
            </div>

            <div className="chart-panel">
              <div className="chart-panel-header">
                <div style={{ display: 'flex', alignItems: 'baseline', gap: 12 }}>
                  <span className="section-number">03</span>
                  <span className="chart-panel-title">Live Feed</span>
                </div>
                <div className="connection-status" style={{ gap: 6 }}>
                  <span
                    className={`connection-indicator ${connected ? 'connected' : ''}`}
                    style={{ width: 4, height: 4 }}
                  />
                </div>
              </div>
              <AlertFeed alerts={alerts} />
            </div>
          </div>
        </div>

        {/* 04 — TRANSACTION LOG */}
        <div className="section stagger-5">
          <div className="section-header">
            <span className="section-number">04</span>
            <span className="section-title">Transaction Log</span>
          </div>
          <TransactionTable
            transactions={transactions}
            total={total}
            page={page}
            totalPages={totalPages}
            filter={filter}
            onFilterChange={handleFilterChange}
            onPageChange={setPage}
          />
        </div>
      </main>
    </div>
  );
}

export default App;
