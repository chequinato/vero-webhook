import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type { AlertItem, TransacaoDto } from './types';

const HUB_URL = import.meta.env.VITE_API_URL
  ? `${import.meta.env.VITE_API_URL}/hubs/transactions`
  : 'http://localhost:5000/hubs/transactions';

export function useSignalR(onNewTransaction: () => void) {
  const [connected, setConnected] = useState(false);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const addAlert = useCallback((alert: AlertItem) => {
    setAlerts(prev => [alert, ...prev].slice(0, 50)); // Manter últimos 50
  }, []);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('TransacaoRecebida', (tx: TransacaoDto) => {
      const riskLevel = (tx.riskScore ?? 0) > 0.7 ? 'danger'
        : (tx.riskScore ?? 0) > 0.4 ? 'warning' : 'success';

      addAlert({
        id: crypto.randomUUID(),
        transacaoId: tx.id,
        motivo: tx.status === 'bloqueada' ? tx.motivo ?? 'bloqueada' : tx.status,
        type: tx.status === 'bloqueada' ? 'danger' : riskLevel,
        message: tx.status === 'bloqueada'
          ? `Transação ${tx.id} BLOQUEADA — ${tx.motivo}`
          : `Transação ${tx.id} recebida — R$ ${tx.valor.toLocaleString('pt-BR')} (risk: ${((tx.riskScore ?? 0) * 100).toFixed(0)}%)`,
        timestamp: new Date(),
      });

      onNewTransaction();
    });

    connection.on('StatusAtualizado', (data: { id: string; status: string; motivo: string | null; riskScore: number | null }) => {
      addAlert({
        id: crypto.randomUUID(),
        transacaoId: data.id,
        motivo: data.motivo ?? data.status,
        type: data.status === 'suspeita' ? 'warning'
          : data.status === 'aprovada' ? 'success' : 'danger',
        message: `Status de ${data.id} → ${data.status.toUpperCase()}${data.motivo ? ` (${data.motivo})` : ''}`,
        timestamp: new Date(),
      });

      onNewTransaction();
    });

    connection.on('AlertaDisparado', (data: { id: string; motivo: string }) => {
      addAlert({
        id: crypto.randomUUID(),
        transacaoId: data.id,
        motivo: data.motivo,
        type: 'danger',
        message: `🚨 ALERTA: Anomalia em ${data.id} — ${data.motivo}`,
        timestamp: new Date(),
      });
    });

    connection.onreconnecting(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));
    connection.onclose(() => setConnected(false));

    connection.start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => {
      connection.stop();
    };
  }, [addAlert, onNewTransaction]);

  return { connected, alerts };
}
