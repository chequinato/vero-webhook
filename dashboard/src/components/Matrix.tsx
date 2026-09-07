import { useMemo, useState } from 'react';
import type { TransacaoDto } from '../types';

/**
 * MATRIZ TIPO × RISCO
 *
 * Uma tabela de contingência impressa: instrumento de papel, não mapa de
 * calor colorido. A intensidade é tinta — do vazio ao sólido — e a cor só
 * aparece nas duas colunas que interessam (revisão e bloqueio), para o
 * olho encontrar o canto quente sem precisar de escala de arco-íris.
 *
 * As células abrem em frente de onda diagonal (linha + coluna), e o cursor
 * acende a linha e a coluna inteiras, como uma régua de leitura.
 */

const FAIXAS = [
  { lbl: '00–20', min: 0.0, max: 0.2, tom: '' },
  { lbl: '20–40', min: 0.2, max: 0.4, tom: '' },
  { lbl: '40–60', min: 0.4, max: 0.6, tom: 'amber' },
  { lbl: '60–80', min: 0.6, max: 0.8, tom: 'amber' },
  { lbl: '80–100', min: 0.8, max: 1.01, tom: 'signal' },
];

export function Matrix({ amostra }: { amostra: TransacaoDto[] }) {
  const [foco, setFoco] = useState<{ r: number; c: number } | null>(null);

  const { tipos, grade, max, totalLinha, totalColuna, total } = useMemo(() => {
    const tipos = [...new Set(amostra.map(t => t.tipo || '—'))].sort();
    const grade = tipos.map(() => new Array<number>(FAIXAS.length).fill(0));

    for (const t of amostra) {
      const r = tipos.indexOf(t.tipo || '—');
      if (r < 0) continue;
      const s = t.riskScore ?? 0;
      const c = FAIXAS.findIndex(f => s >= f.min && s < f.max);
      if (c >= 0) grade[r][c]++;
    }

    const max = Math.max(1, ...grade.flat());
    const totalLinha = grade.map(l => l.reduce((a, v) => a + v, 0));
    const totalColuna = FAIXAS.map((_, c) => grade.reduce((a, l) => a + l[c], 0));
    return { tipos, grade, max, totalLinha, totalColuna, total: totalLinha.reduce((a, v) => a + v, 0) };
  }, [amostra]);

  if (tipos.length === 0) {
    return <div className="painel-vazio">Sem amostra para cruzar</div>;
  }

  return (
    <div className="matriz" onMouseLeave={() => setFoco(null)}>
      <div
        className="matriz-grade"
        style={{ gridTemplateColumns: `74px repeat(${FAIXAS.length}, minmax(0, 1fr)) 46px` }}
      >
        {/* cabeçalho */}
        <span className="matriz-canto">tipo ╲ risco</span>
        {FAIXAS.map((f, c) => (
          <span key={f.lbl} className={`matriz-col${foco?.c === c ? ' on' : ''}`}>{f.lbl}</span>
        ))}
        <span className="matriz-col matriz-col--soma">Σ</span>

        {/* corpo */}
        {tipos.map((tipo, r) => (
          <Linha
            key={tipo}
            tipo={tipo}
            linha={grade[r]}
            r={r}
            max={max}
            soma={totalLinha[r]}
            foco={foco}
            setFoco={setFoco}
          />
        ))}

        {/* rodapé de somas */}
        <span className="matriz-lin matriz-lin--soma">Σ</span>
        {totalColuna.map((v, c) => (
          <span key={c} className={`matriz-soma${foco?.c === c ? ' on' : ''}`}>{v}</span>
        ))}
        <span className="matriz-soma matriz-soma--total">{total}</span>
      </div>

      <div className="matriz-escala">
        <span>vazio</span>
        {[0.08, 0.28, 0.5, 0.72, 1].map(v => (
          <i key={v} style={{ ['--v' as string]: v }} />
        ))}
        <span>{max}</span>
      </div>
    </div>
  );
}

function Linha({ tipo, linha, r, max, soma, foco, setFoco }: {
  tipo: string;
  linha: number[];
  r: number;
  max: number;
  soma: number;
  foco: { r: number; c: number } | null;
  setFoco: (f: { r: number; c: number } | null) => void;
}) {
  return (
    <>
      <span className={`matriz-lin${foco?.r === r ? ' on' : ''}`}>{tipo}</span>
      {linha.map((v, c) => {
        const i = v / max;
        const naLinha = foco?.r === r;
        const naColuna = foco?.c === c;
        return (
          <span
            key={c}
            className={[
              'matriz-cel',
              FAIXAS[c].tom ? `matriz-cel--${FAIXAS[c].tom}` : '',
              i > 0.55 ? 'matriz-cel--densa' : '',
              naLinha || naColuna ? 'regua' : '',
            ].filter(Boolean).join(' ')}
            style={{
              ['--v' as string]: i.toFixed(3),
              animationDelay: `${(r + c) * 42}ms`,
            }}
            onMouseEnter={() => setFoco({ r, c })}
            title={`${tipo} · risco ${FAIXAS[c].lbl} · ${v}`}
          >
            {v > 0 ? v : ''}
          </span>
        );
      })}
      <span className={`matriz-soma${foco?.r === r ? ' on' : ''}`}>{soma}</span>
    </>
  );
}
