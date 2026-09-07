import { useState } from 'react';
import { Odometer } from './Odometer';

/**
 * ROSCA DE COMPOSIÇÃO — desenhada como mostrador de instrumento.
 *
 * Não é um gráfico de pizza com legenda ao lado: é um anel gravado. A
 * escala externa tem traços a cada 2,5% (traço longo a cada 10%), os
 * setores são traçados um a um no sentido horário e o setor sob o cursor
 * sai do anel na direção da própria bissetriz — o gesto de um seletor
 * mecânico, não um "hover com sombra".
 *
 * O centro é a leitura: sem cursor mostra o total; com cursor, o setor.
 */

export interface Fatia {
  key: string;
  label: string;
  value: number;
  cor: string;
}

const TAM = 268;
const C = TAM / 2;
const R = 92;          // raio do meio do anel
const ESP = 26;        // espessura do anel
const VAO = 2.2;       // vão angular entre setores, em graus
const R_ESCALA = R + ESP / 2 + 12;

const rad = (g: number) => ((g - 90) * Math.PI) / 180;
const px = (g: number, r: number) => [C + r * Math.cos(rad(g)), C + r * Math.sin(rad(g))] as const;

function arco(a0: number, a1: number, r: number): string {
  const [x0, y0] = px(a0, r);
  const [x1, y1] = px(a1, r);
  const grande = a1 - a0 > 180 ? 1 : 0;
  return `M${x0.toFixed(2)} ${y0.toFixed(2)} A${r} ${r} 0 ${grande} 1 ${x1.toFixed(2)} ${y1.toFixed(2)}`;
}

export function Donut({ fatias, total, unidade = 'transações' }: {
  fatias: Fatia[];
  total: number;
  unidade?: string;
}) {
  const [ativo, setAtivo] = useState<number | null>(null);

  const soma = fatias.reduce((a, f) => a + f.value, 0) || 1;

  // Laço explícito: o ângulo acumulado é estado do desenho, não do React.
  const setores: Array<Fatia & { a0: number; a1: number; meio: number; pct: number }> = [];
  let cursor = 0;
  for (const f of fatias) {
    const span = (f.value / soma) * 360;
    const a0 = cursor + VAO / 2;
    const a1 = Math.max(a0 + 0.4, cursor + span - VAO / 2);
    cursor += span;
    setores.push({ ...f, a0, a1, meio: (a0 + a1) / 2, pct: (f.value / soma) * 100 });
  }

  const sel = ativo !== null ? setores[ativo] : null;

  return (
    <div className="donut">
      <div className="donut-anel">
      <svg viewBox={`0 0 ${TAM} ${TAM}`} className="donut-svg" role="img"
           aria-label={`Composição: ${setores.map(s => `${s.label} ${s.pct.toFixed(1)}%`).join(', ')}`}>
        {/* ── Escala gravada ── */}
        <g className="donut-escala">
          {Array.from({ length: 40 }, (_, i) => {
            const g = i * 9;
            const longo = i % 4 === 0;
            const [x0, y0] = px(g, R_ESCALA);
            const [x1, y1] = px(g, R_ESCALA + (longo ? 8 : 4));
            return (
              <line
                key={i}
                x1={x0} y1={y0} x2={x1} y2={y1}
                strokeWidth={longo ? 1.4 : 1}
                opacity={longo ? 0.85 : 0.4}
                style={{ animationDelay: `${i * 9}ms` }}
              />
            );
          })}
        </g>

        {/* ── Trilho do anel ── */}
        <circle className="donut-trilho" cx={C} cy={C} r={R} strokeWidth={ESP} fill="none" />

        {/* ── Setores ── */}
        {setores.map((s, i) => {
          const fora = ativo === i;
          const [dx, dy] = px(s.meio, 5);
          return (
            <path
              key={s.key}
              className="donut-setor"
              d={arco(s.a0, s.a1, R)}
              stroke={s.cor}
              strokeWidth={ESP}
              fill="none"
              pathLength={100}
              onMouseEnter={() => setAtivo(i)}
              onMouseLeave={() => setAtivo(null)}
              opacity={ativo !== null && !fora ? 0.28 : 1}
              style={{
                animationDelay: `${180 + i * 110}ms`,
                transform: fora ? `translate(${(dx - C).toFixed(2)}px, ${(dy - C).toFixed(2)}px)` : undefined,
              }}
            />
          );
        })}

        {/* ── Agulha do setor selecionado: fio até a escala ── */}
        {sel && (() => {
          const [x0, y0] = px(sel.meio, R + ESP / 2 + 6);
          const [x1, y1] = px(sel.meio, R_ESCALA + 14);
          return <line className="donut-agulha" x1={x0} y1={y0} x2={x1} y2={y1} />;
        })()}
      </svg>

      {/* ── Leitura central ── */}
      <div className="donut-centro">
        <span className="donut-centro-num">
          {sel
            ? <Odometer value={sel.pct} decimals={1} suffix="%" />
            : <Odometer value={total} />}
        </span>
        <span className="donut-centro-lbl">{sel ? sel.label : unidade}</span>
        {sel && (
          <span className="donut-centro-abs">
            {sel.value.toLocaleString('pt-BR')} de {soma.toLocaleString('pt-BR')}
          </span>
        )}
      </div>
      </div>

      {/* ── Chaves: tabela, não legenda de biblioteca ── */}
      <div className="donut-chaves">
        {setores.map((s, i) => (
          <button
            key={s.key}
            className={`donut-chave${ativo === i ? ' on' : ''}`}
            onMouseEnter={() => setAtivo(i)}
            onMouseLeave={() => setAtivo(null)}
            onFocus={() => setAtivo(i)}
            onBlur={() => setAtivo(null)}
            style={{ ['--i' as string]: 6 + i }}
          >
            <i style={{ background: s.cor }} />
            <span className="donut-chave-lbl">{s.label}</span>
            <span className="donut-chave-pct">{s.pct.toFixed(1)}%</span>
            <span className="donut-chave-abs">{s.value.toLocaleString('pt-BR')}</span>
          </button>
        ))}
      </div>
    </div>
  );
}
