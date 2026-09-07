import { useEffect, useState } from 'react';

/**
 * MANÔMETRO — arco de 240°, escala gravada, ponteiro que assenta uma vez.
 *
 * Deliberadamente analógico: o valor exato está escrito embaixo, então o
 * arco não precisa ser preciso, precisa ser *lido de longe*. O ponteiro
 * usa a curva mecânica e não oscila; o setor percorrido é traçado atrás
 * dele. A zona vermelha é impressa na escala, não acesa quando ativa.
 */

const TAM = 152;
const C = TAM / 2;
const R = 56;
const A0 = -120;
const A1 = 120;

const rad = (g: number) => ((g - 90) * Math.PI) / 180;
const px = (g: number, r: number) => [C + r * Math.cos(rad(g)), C + r * Math.sin(rad(g))] as const;

function arco(a0: number, a1: number, r: number) {
  const [x0, y0] = px(a0, r);
  const [x1, y1] = px(a1, r);
  return `M${x0.toFixed(2)} ${y0.toFixed(2)} A${r} ${r} 0 ${a1 - a0 > 180 ? 1 : 0} 1 ${x1.toFixed(2)} ${y1.toFixed(2)}`;
}

interface Props {
  /** 0–1. Fora do intervalo é grampeado; o rótulo mostra o valor bruto. */
  fracao: number;
  leitura: string;
  legenda: string;
  /** Início da zona de atenção impressa no arco, em fração. */
  zona?: number;
  tom?: 'neutro' | 'amber' | 'signal' | 'verde';
}

export function Gauge({ fracao, leitura, legenda, zona = 0.75, tom = 'neutro' }: Props) {
  const f = Math.max(0, Math.min(1, fracao));
  const [assentado, setAssentado] = useState(0);

  useEffect(() => {
    // Mesma rede de segurança do contador: sem ela o ponteiro fica preso
    // no zero da escala quando o rAF está estrangulado.
    const quadro = requestAnimationFrame(() => setAssentado(f));
    const rede = setTimeout(() => setAssentado(f), 48);
    return () => {
      cancelAnimationFrame(quadro);
      clearTimeout(rede);
    };
  }, [f]);

  const ang = A0 + assentado * (A1 - A0);

  return (
    <div className={`gauge gauge--${tom}`}>
      <svg viewBox={`0 0 ${TAM} ${TAM}`} width={TAM} height={TAM} aria-hidden="true">
        {/* trilho */}
        <path className="gauge-trilho" d={arco(A0, A1, R)} strokeWidth={9} fill="none" />

        {/* zona de atenção impressa */}
        <path
          className="gauge-zona"
          d={arco(A0 + zona * (A1 - A0), A1, R)}
          strokeWidth={9}
          fill="none"
        />

        {/* percurso */}
        <path
          className="gauge-percurso"
          d={arco(A0, Math.max(A0 + 0.5, ang), R)}
          strokeWidth={9}
          fill="none"
          pathLength={100}
        />

        {/* escala */}
        <g className="gauge-escala">
          {Array.from({ length: 21 }, (_, i) => {
            const g = A0 + (i / 20) * (A1 - A0);
            const longo = i % 5 === 0;
            const [x0, y0] = px(g, R + 7);
            const [x1, y1] = px(g, R + (longo ? 15 : 11));
            return <line key={i} x1={x0} y1={y0} x2={x1} y2={y1} strokeWidth={longo ? 1.4 : 1} opacity={longo ? 0.9 : 0.4} />;
          })}
        </g>

        {/* ponteiro */}
        <g
          className="gauge-ponteiro"
          style={{ transform: `rotate(${ang.toFixed(2)}deg)`, transformOrigin: `${C}px ${C}px` }}
        >
          <line x1={C} y1={C + 8} x2={C} y2={C - R + 12} strokeWidth={2} />
          <circle cx={C} cy={C} r={3.5} />
        </g>
      </svg>

      <div className="gauge-leitura">
        <b>{leitura}</b>
        <span>{legenda}</span>
      </div>
    </div>
  );
}
