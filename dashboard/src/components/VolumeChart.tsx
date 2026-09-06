import { Activity } from 'lucide-react';
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend
} from 'recharts';
import type { VolumeHora } from '../types';

interface VolumeChartProps {
  data: VolumeHora[];
}

export function VolumeChart({ data }: VolumeChartProps) {
  const chartData = data.map(v => ({
    hora: new Date(v.hora).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
    total: v.quantidade,
    bloqueadas: v.bloqueadas,
    suspeitas: v.suspeitas,
    normais: v.quantidade - v.bloqueadas - v.suspeitas,
  }));

  if (chartData.length === 0) {
    return (
      <div className="empty-state">
        <Activity size={32} />
        <p>Sem dados de volume nas últimas 24h</p>
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={260}>
      <AreaChart data={chartData}>
        <defs>
          <linearGradient id="colorNormais" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#3b82f6" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="colorBloqueadas" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#ef4444" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#ef4444" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="colorSuspeitas" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%" stopColor="#f59e0b" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#f59e0b" stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid strokeDasharray="3 3" stroke="#334155" />
        <XAxis dataKey="hora" stroke="#64748b" fontSize={11} />
        <YAxis stroke="#64748b" fontSize={11} />
        <Tooltip
          contentStyle={{
            background: '#1e293b',
            border: '1px solid #334155',
            borderRadius: '8px',
            fontSize: '12px',
          }}
        />
        <Legend iconSize={8} wrapperStyle={{ fontSize: '12px' }} />
        <Area type="monotone" dataKey="normais" name="Normais"
          stroke="#3b82f6" fill="url(#colorNormais)" strokeWidth={2} />
        <Area type="monotone" dataKey="bloqueadas" name="Bloqueadas"
          stroke="#ef4444" fill="url(#colorBloqueadas)" strokeWidth={2} />
        <Area type="monotone" dataKey="suspeitas" name="Suspeitas"
          stroke="#f59e0b" fill="url(#colorSuspeitas)" strokeWidth={2} />
      </AreaChart>
    </ResponsiveContainer>
  );
}
