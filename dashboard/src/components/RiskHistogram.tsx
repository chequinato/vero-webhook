import { useMemo, useState } from 'react';
import { useWidth } from '../hooks/useWidth';
import type { TransacaoDto } from '../types';

/**
 * DISTRIBUIÇÃO DE RISCO
 *
 * Vinte faixas de 5 pontos percentuais. O que faz este gráfico valer a
 * página não são as barras — é o que está impresso por cima delas: os
 * dois cortes de decisão do motor (40% revisão, 70% bloqueio) entram como
 * fios verticais rotulados, então dá para ver quanta massa está encostada
 * em cada corte. Uma acumulada é traçada por cima, na escala da direita.
 *
 * As barras crescem da base em frente de onda da esquerda para a direita.
 */

const H = 194;
const PAD = { top: 16, right: 40, bottom: 26, left: 34 };
const FAIXAS = 20;

const CORTE_REVISAO = 0.4;
const CORTE_BLOQUEIO = 0.7;

export function RiskHistogram({
  amostra,
  limiar,
}: {
  amostra: TransacaoDto[];
  /** Corte simulado pela curva de operação logo abaixo, em pontos (0–100). */
  limiar?: number;
}) {
  const [ref, w] = useWidth(520);
  const [ativo, setAtivo] = useState<number | null>(null);

  const { baldes, maxN, acumulada, n } = useMemo(() => {
    const b = new Array<number>(FAIXAS).fill(0);
    let n = 0;
    for (const t of amostra) {
      if (t.riskScore === null || t.riskScore === undefined) continue;
      const i = Math.min(FAIXAS - 1, Math.max(0, Math.floor(t.riskScore * FAIXAS)));
      b[i]++;
      n++;
    }
    const ac: number[] = [];
    let acc = 0;
    for (const v of b) {
      acc += v;
      ac.push(n > 0 ? acc / n : 0);
    }
    return { baldes: b, maxN: Math.max(...b, 1), acumulada: ac, n };
  }, [amostra]);

  if (n === 0) {
    return <div className="painel-vazio">Sem escores de risco na amostra</div>;
  }

  const plotW = w - PAD.left - PAD.right;
  const plotH = H - PAD.top - PAD.bottom;
  const passo = plotW / FAIXAS;
  const barW = Math.max(2, passo - 3);

  const x = (i: number) => PAD.left + i * passo;
  const y = (v: number) => PAD.top + plotH - (v / maxN) * plotH;
  const yAc = (v: number) => PAD.top + plotH - v * plotH;

  const corte = (v: number) => PAD.left + v * plotW;

  const tom = (i: number) => {
    const centro = (i + 0.5) / FAIXAS;
    return centro >= CORTE_BLOQUEIO ? 'var(--signal)'
      : centro >= CORTE_REVISAO ? 'var(--amber)'
      : 'var(--verde)';
  };

  const dAc = acumulada
    .map((v, i) => `${i === 0 ? 'M' : 'L'}${(x(i) + passo / 2).toFixed(1)} ${yAc(v).toFixed(1)}`)
    .join(' ');

  const sel = ativo !== null ? ativo : null;
  const massaAcima = baldes.slice(Math.ceil(CORTE_BLOQUEIO * FAIXAS)).reduce((a, v) => a + v, 0);

  return (
    <div className="hist" ref={ref}>
      <div className="hist-readout">
        {sel !== null ? (
          <>
            <span>faixa <b>{(sel * 5).toString().padStart(2, '0')}–{sel * 5 + 5}%</b></span>
            <span>n <b>{baldes[sel]}</b></span>
            <span>acum <b>{(acumulada[sel] * 100).toFixed(1)}%</b></span>
          </>
        ) : (
          <>
            <span>amostra <b>{n}</b></span>
            <span>acima do corte <b>{massaAcima}</b></span>
          </>
        )}
      </div>

      <svg viewBox={`0 0 ${w} ${H}`} width={w} height={H} onMouseLeave={() => setAtivo(null)}>
        {/* grade horizontal */}
        {[0, 0.5, 1].map(k => (
          <line key={k} className="hist-grid"
                x1={PAD.left} x2={w - PAD.right} y1={y(maxN * k)} y2={y(maxN * k)} />
        ))}

        {/* barras */}
        {baldes.map((v, i) => {
          const h = (v / maxN) * plotH;
          return (
            <g key={i} onMouseEnter={() => setAtivo(i)}>
              {/* alvo de cursor de coluna inteira, invisível */}
              <rect x={x(i)} y={PAD.top} width={passo} height={plotH} fill="transparent" />
              {v > 0 && (
                <rect
                  className="hist-bar"
                  x={x(i) + (passo - barW) / 2}
                  y={y(v)}
                  width={barW}
                  height={Math.max(1, h)}
                  fill={tom(i)}
                  opacity={sel !== null && sel !== i ? 0.3 : 0.92}
                  style={{ animationDelay: `${i * 26}ms` }}
                />
              )}
            </g>
          );
        })}

        {/* acumulada */}
        <path className="hist-ac" d={dAc} pathLength={100} />

        {/* Corte simulado: a mesma vertical que o operador arrasta na curva
            de operação. A região à direita é o que ficaria retido. */}
        {limiar !== undefined && (
          <g className="hist-sim">
            <rect
              x={corte(limiar / 100)}
              y={PAD.top}
              width={Math.max(0, w - PAD.right - corte(limiar / 100))}
              height={plotH}
            />
            <line x1={corte(limiar / 100)} x2={corte(limiar / 100)} y1={PAD.top - 10} y2={PAD.top + plotH} />
          </g>
        )}

        {/* cortes de decisão */}
        {[
          { v: CORTE_REVISAO, txt: 'revisão 40', cls: 'amber' },
          { v: CORTE_BLOQUEIO, txt: 'bloqueio 70', cls: 'signal' },
        ].map(c => (
          <g key={c.txt} className={`hist-corte hist-corte--${c.cls}`}>
            <line x1={corte(c.v)} x2={corte(c.v)} y1={PAD.top - 6} y2={PAD.top + plotH} />
            <text x={corte(c.v) + 5} y={PAD.top + 3} fontSize="10">{c.txt}</text>
          </g>
        ))}

        {/* base + eixo */}
        <line className="hist-base" x1={PAD.left} x2={w - PAD.right}
              y1={PAD.top + plotH} y2={PAD.top + plotH} />

        {[0, 25, 50, 75, 100].map(p => (
          <text key={p} className="hist-tick" x={PAD.left + (p / 100) * plotW} y={H - 8}
                textAnchor={p === 0 ? 'start' : p === 100 ? 'end' : 'middle'} fontSize="10">
            {p}%
          </text>
        ))}

        {/* escala esquerda (contagem) e direita (acumulada) */}
        <text className="hist-tick" x={PAD.left - 7} y={y(maxN) + 4} textAnchor="end" fontSize="10">{maxN}</text>
        <text className="hist-tick" x={PAD.left - 7} y={y(0) + 4} textAnchor="end" fontSize="10">0</text>
        <text className="hist-tick" x={w - PAD.right + 7} y={yAc(1) + 4} fontSize="10">100</text>
        <text className="hist-tick" x={w - PAD.right + 7} y={yAc(0) + 4} fontSize="10">0</text>
      </svg>
    </div>
  );
}
