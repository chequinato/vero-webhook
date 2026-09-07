import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, Cell
} from 'recharts';
import type { VolumeHora } from '../types';

interface VolumeChartProps {
  data: VolumeHora[];
}

interface ChartPoint {
  hora: string;
  normais: number;
  bloqueadas: number;
  suspeitas: number;
  total: number;
}

function CustomTooltip({ active, payload, label }: { active?: boolean; payload?: Array<{ name: string; value: number; color: string }>; label?: string }) {
  if (!active || !payload?.length) return null;
  return (
    <div style={{
      background: '#111116',
      border: '1px solid #222228',
      padding: '10px 14px',
      fontFamily: 'var(--font-mono)',
      fontSize: 11,
      lineHeight: 1.6,
    }}>
      <div style={{ color: '#88888f', marginBottom: 4, letterSpacing: '0.08em', textTransform: 'uppercase' as const, fontSize: 9 }}>
        {label}
      </div>
      {payload.map((item, i) => (
        <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
          <span style={{ width: 4, height: 4, background: item.color, display: 'inline-block' }} />
          <span style={{ color: '#88888f' }}>{item.name}</span>
          <span style={{ color: '#e4e4e8', fontWeight: 600, marginLeft: 'auto', paddingLeft: 16 }}>{item.value}</span>
        </div>
      ))}
    </div>
  );
}

export function VolumeChart({ data }: VolumeChartProps) {
  const chartData: ChartPoint[] = data.map(v => ({
    hora: new Date(v.hora).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
    total: v.quantidade,
    bloqueadas: v.bloqueadas,
    suspeitas: v.suspeitas,
    normais: v.quantidade - v.bloqueadas - v.suspeitas,
  }));

  if (chartData.length === 0) {
    return (
      <div className="empty-state">
        <div style={{ width: 6, height: 6, background: 'var(--text-muted)', margin: '0 auto 12px', animation: 'pulse-subtle 2s ease-in-out infinite' }} />
        <span>Aguardando dados de volume</span>
      </div>
    );
  }

  return (
    <ResponsiveContainer width="100%" height={240}>
      <BarChart data={chartData} barGap={0} barCategoryGap="20%">
        <XAxis
          dataKey="hora"
          stroke="#2e2e36"
          fontSize={9}
          fontFamily="'JetBrains Mono', monospace"
          tick={{ fill: '#4a4a52' }}
          tickLine={false}
          axisLine={{ stroke: '#222228' }}
          interval="preserveStartEnd"
        />
        <YAxis
          stroke="#2e2e36"
          fontSize={9}
          fontFamily="'JetBrains Mono', monospace"
          tick={{ fill: '#4a4a52' }}
          tickLine={false}
          axisLine={false}
          width={32}
        />
        <Tooltip content={<CustomTooltip />} cursor={{ fill: 'rgba(255,255,255,0.02)' }} />
        <Bar
          dataKey="normais"
          name="Normal"
          stackId="stack"
          fill="#00dfa2"
          radius={0}
          animationDuration={800}
          animationBegin={200}
        >
          {chartData.map((_, idx) => (
            <Cell key={idx} fillOpacity={0.7} />
          ))}
        </Bar>
        <Bar
          dataKey="bloqueadas"
          name="Bloqueada"
          stackId="stack"
          fill="#ff4040"
          radius={0}
          animationDuration={800}
          animationBegin={400}
        >
          {chartData.map((_, idx) => (
            <Cell key={idx} fillOpacity={0.8} />
          ))}
        </Bar>
        <Bar
          dataKey="suspeitas"
          name="Suspeita"
          stackId="stack"
          fill="#dfa000"
          radius={0}
          animationDuration={800}
          animationBegin={600}
        >
          {chartData.map((_, idx) => (
            <Cell key={idx} fillOpacity={0.8} />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}
