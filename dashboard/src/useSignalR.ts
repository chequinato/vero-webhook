import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { IS_DEMO } from './api';
import { demo } from './demo';
import { registrarEvento } from './telemetry';
import type { AlertItem, TransacaoDto, StatusPatch } from './types';

const HUB_URL = import.meta.env.VITE_API_URL
  ? `${import.meta.env.VITE_API_URL}/hubs/transactions`
  : 'http://localhost:5000/hubs/transactions';

/**
 * O hub entrega o objeto inteiro, não só um aviso de "chegou algo". O painel
 * usa isso para aplicar a mudança localmente no mesmo quadro em que o evento
 * chega — o contador rola, a coluna da hora cresce e a linha aparece no livro
 * sem esperar o próximo ciclo de busca. A reconciliação com o servidor vem
 * depois, sem que o operador perceba.
 */
interface Ouvintes {
  onTransacao: (tx: TransacaoDto) => void;
  onStatus: (patch: StatusPatch) => void;
}

export function useSignalR({ onTransacao, onStatus }: Ouvintes) {
  const [connected, setConnected] = useState(false);
  const [alerts, setAlerts] = useState<AlertItem[]>([]);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Os retornos ficam numa referência para o efeito não remontar a conexão
  // toda vez que o componente pai renderiza.
  const ouvintes = useRef<Ouvintes>({ onTransacao, onStatus });
  useEffect(() => {
    ouvintes.current = { onTransacao, onStatus };
  }, [onTransacao, onStatus]);

  const addAlert = useCallback((alert: AlertItem) => {
    // Todo evento que entra alimenta a vazão do painel de observabilidade.
    registrarEvento();
    setAlerts(prev => [alert, ...prev].slice(0, 50)); // Manter últimos 50
  }, []);

  useEffect(() => {
    // ── Modo demonstração: fita sintética, sem hub ──
    if (IS_DEMO) {
      setConnected(true);
      let timer = 0;

      // Intervalo irregular: um fluxo real não chega em metrônomo, e um
      // painel que pisca em cadência fixa denuncia que é simulação.
      const agenda = () => {
        timer = window.setTimeout(() => {
          const tx = demo.evento();
          addAlert(montaAlerta(tx));
          ouvintes.current.onTransacao(tx);
          agenda();
        }, 700 + Math.random() * 1800);
      };

      agenda();
      return () => clearTimeout(timer);
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connectionRef.current = connection;

    connection.on('TransacaoRecebida', (tx: TransacaoDto) => {
      addAlert(montaAlerta(tx));
      ouvintes.current.onTransacao(tx);
    });

    connection.on('StatusAtualizado', (data: StatusPatch) => {
      addAlert({
        id: crypto.randomUUID(),
        transacaoId: data.id,
        motivo: data.motivo ?? data.status,
        type: data.status === 'suspeita' ? 'warning'
          : data.status === 'aprovada' ? 'success' : 'danger',
        message: `Status de ${data.id} → ${data.status.toUpperCase()}${data.motivo ? ` (${data.motivo})` : ''}`,
        timestamp: new Date(),
      });

      ouvintes.current.onStatus(data);
    });

    connection.on('AlertaDisparado', (data: { id: string; motivo: string }) => {
      addAlert({
        id: crypto.randomUUID(),
        transacaoId: data.id,
        motivo: data.motivo,
        type: 'danger',
        message: `ALERTA: anomalia em ${data.id} — ${data.motivo}`,
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
  }, [addAlert]);

  return { connected, alerts };
}

function montaAlerta(tx: TransacaoDto): AlertItem {
  const risco = tx.riskScore ?? 0;
  const nivel = risco > 0.7 ? 'danger' : risco > 0.4 ? 'warning' : 'success';

  return {
    id: crypto.randomUUID(),
    transacaoId: tx.id,
    // A etiqueta já diz o estado; aqui vai só o que ela não diz.
    motivo: tx.motivo ?? `R$ ${tx.valor.toLocaleString('pt-BR')} · ${(risco * 100).toFixed(0)}%`,
    type: tx.status === 'bloqueada' ? 'danger' : nivel,
    message: tx.status === 'bloqueada'
      ? `Transação ${tx.id} BLOQUEADA — ${tx.motivo}`
      : `Transação ${tx.id} recebida — R$ ${tx.valor.toLocaleString('pt-BR')} (risco: ${(risco * 100).toFixed(0)}%)`,
    timestamp: new Date(),
  };
}
