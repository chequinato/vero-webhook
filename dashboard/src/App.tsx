import { useState, useEffect, useCallback } from 'react';
import { Shield } from 'lucide-react';
import { api } from './api';
import { useSignalR } from './useSignalR';
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

  // Auto-refresh a cada 10 segundos
  useEffect(() => {
    const interval = setInterval(() => {
      setRefreshKey(k => k + 1);
    }, 10000);
    return () => clearInterval(interval);
  }, []);

  // ─── SignalR (real-time) ───
  const handleNewTransaction = useCallback(() => {
    setRefreshKey(k => k + 1);
  }, []);

  const { connected, alerts } = useSignalR(handleNewTransaction);

  // ─── Filter / Pagination handlers ───
  const handleFilterChange = (f: string) => {
    setFilter(f);
    setPage(1);
  };

  return (
    <div className="app">
      {/* Header */}
      <header className="header">
        <div className="header-left">
          <div className="header-logo">
            <Shield size={18} />
          </div>
          <h1>
            Vero
            <span>Fraud Detection Dashboard</span>
          </h1>
        </div>
        <div className="connection-status">
          <span className={`connection-dot ${connected ? 'connected' : ''}`} />
          {connected ? 'Live' : 'Offline'}
        </div>
      </header>

      {/* Main Content */}
      <main className="main">
        {/* Stats Cards */}
        <StatsCards stats={stats} loading={loading} />

        {/* Charts Row */}
        <div className="charts-row">
          <div className="chart-card">
            <h3>📈 Volume de Transações (24h)</h3>
            <VolumeChart data={timeline} />
          </div>
          <div className="chart-card">
            <h3>🔔 Feed em Tempo Real</h3>
            <AlertFeed alerts={alerts} />
          </div>
        </div>

        {/* Transaction Table */}
        <TransactionTable
          transactions={transactions}
          total={total}
          page={page}
          totalPages={totalPages}
          filter={filter}
          onFilterChange={handleFilterChange}
          onPageChange={setPage}
        />
      </main>
    </div>
  );
}

export default App;
