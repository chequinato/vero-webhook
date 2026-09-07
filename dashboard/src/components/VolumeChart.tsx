import { useEffect, useMemo, useRef, useState } from 'react';
import type { VolumeHora } from '../types';

/**
 * Gráfico de fluxo — SVG próprio, sem biblioteca.
 *
 * Colunas empilhadas crescem da linha de base com 16ms de defasagem entre elas;
 * a linha de taxa de bloqueio é traçada por cima. O cursor move um fio vertical
 * e alimenta a leitura no canto — comportamento de instrumento, não de tooltip.
 */

const H = 250;
const PAD = { top: 14, right: 10, bottom: 28, left: 42 };

interface Point {
  label: string;
  total: number;
  normais: number;
  bloqueadas: number;
  suspeitas: number;
  taxa: number;
}

export function VolumeChart({ data }: { data: VolumeHora[] }) {
  const wrapRef = useRef<HTMLDivElement>(null);
  const [w, setW] = useState(720);
  const [hover, setHover] = useState<number | null>(null);

  useEffect(() => {
    const el = wrapRef.current;
    if (!el) return;
    const ro = new ResizeObserver(([entry]) => setW(entry.contentRect.width));
    ro.observe(el);
    setW(el.clientWidth);
    return () => ro.disconnect();
  }, []);

  const points: Point[] = useMemo(
    () =>
      data.map(v => {
        const bloqueadas = v.bloqueadas;
        const suspeitas = v.suspeitas;
        return {
          label: new Date(v.hora).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
          total: v.quantidade,
          bloqueadas,
          suspeitas,
          normais: Math.max(0, v.quantidade - bloqueadas - suspeitas),
          taxa: v.quantidade > 0 ? (bloqueadas + suspeitas) / v.quantidade : 0,
        };
      }),
    [data],
  );

  if (points.length === 0) {
    return (
      <div className="chart-empty">
        <i />
        Sem fluxo registrado — aguardando transações
      </div>
    );
  }

  const plotW = Math.max(120, w - PAD.left - PAD.right);
  const plotH = H - PAD.top - PAD.bottom;
  const n = points.length;
  const colW = plotW / n;
  const barW = Math.max(2, Math.min(colW * 0.6, 26));

  const rawMax = Math.max(...points.map(p => p.total), 1);
  const step = niceStep(rawMax / 4);
  const max = Math.max(step * 4, Math.ceil(rawMax / step) * step);

  const x = (i: number) => PAD.left + colW * i + colW / 2;
  const y = (v: number) => PAD.top + plotH - (v / max) * plotH;

  const gridValues = [0, 1, 2, 3, 4].map(k => k * (max / 4));

  // Linha de taxa — escala própria, ancorada no topo do gráfico.
  const maxTaxa = Math.max(...points.map(p => p.taxa), 0.1);
  const lineY = (t: number) => PAD.top + plotH - (t / maxTaxa) * plotH * 0.72;
  const linePts = points.map((p, i) => [x(i), lineY(p.taxa)] as const);
  const lineD = linePts.map(([px, py], i) => `${i === 0 ? 'M' : 'L'}${px.toFixed(1)} ${py.toFixed(1)}`).join(' ');
  const lineLen = linePts.reduce(
    (acc, cur, i) => (i === 0 ? 0 : acc + Math.hypot(cur[0] - linePts[i - 1][0], cur[1] - linePts[i - 1][1])),
    0,
  );

  const labelEvery = Math.max(1, Math.ceil(n / 9));
  const active = hover !== null ? points[hover] : null;

  const onMove = (e: React.MouseEvent<SVGSVGElement>) => {
    const rect = e.currentTarget.getBoundingClientRect();
    const scale = w / rect.width || 1;
    const px = (e.clientX - rect.left) * scale;
    const i = Math.floor((px - PAD.left) / colW);
    setHover(i >= 0 && i < n ? i : null);
  };

  return (
    <div className="chart" ref={wrapRef}>
      <div className="chart-readout">
        {active ? (
          <>
            <span>{active.label}</span>
            <span>
              fluxo <b>{active.total}</b>
            </span>
            <span>
              blq <b>{active.bloqueadas}</b>
            </span>
            <span>
              sus <b>{active.suspeitas}</b>
            </span>
            <span>
              taxa <b>{(active.taxa * 100).toFixed(1)}%</b>
            </span>
          </>
        ) : (
          <span>
            janela <b>{n}h</b> · pico <b>{rawMax}</b>
          </span>
        )}
      </div>

      <svg
        viewBox={`0 0 ${w} ${H}`}
        width={w}
        height={H}
        onMouseMove={onMove}
        onMouseLeave={() => setHover(null)}
      >
        {/* grade */}
        {gridValues.map((v, i) => (
          <g key={`g${i}`}>
            <line className="chart-grid" x1={PAD.left} x2={w - PAD.right} y1={y(v)} y2={y(v)} strokeWidth="1" />
            <text className="chart-tick" x={PAD.left - 8} y={y(v) + 3} textAnchor="end" fontSize="11">
              {formatTick(v)}
            </text>
          </g>
        ))}

        {/* fio do cursor */}
        {hover !== null && (
          <line className="chart-cursor" x1={x(hover)} x2={x(hover)} y1={PAD.top - 6} y2={PAD.top + plotH} strokeWidth="1" />
        )}

        {/* colunas empilhadas */}
        {points.map((p, i) => {
          const dim = hover !== null && hover !== i;
          const segs: Array<[number, string, number]> = [
            [p.normais, 'var(--zone-fg)', 0.78],
            [p.suspeitas, 'var(--zone-amber)', 0.95],
            [p.bloqueadas, 'var(--zone-signal)', 1],
          ];
          let acc = 0;
          return (
            <g key={i} opacity={dim ? 0.35 : 1} style={{ transition: 'opacity 200ms var(--e-out)' }}>
              {segs.map(([val, fill, op], s) => {
                if (val <= 0) return null;
                const hgt = (val / max) * plotH;
                const ry = y(acc) - hgt;
                acc += val;
                return (
                  <rect
                    key={s}
                    className="chart-col"
                    x={x(i) - barW / 2}
                    y={ry}
                    width={barW}
                    height={Math.max(1, hgt)}
                    fillOpacity={op}
                    style={{
                      fill,
                      animationDelay: `${i * 16}ms`,
                      transition: 'y 320ms var(--e-out), height 320ms var(--e-out)',
                    }}
                  />
                );
              })}
            </g>
          );
        })}

        {/* linha de taxa */}
        <path className="chart-line" d={lineD} style={{ ['--len' as string]: lineLen.toFixed(0) }} />

        {/* marcador do ponto sob o cursor */}
        {hover !== null && (
          <rect
            x={x(hover) - 3}
            y={lineY(points[hover].taxa) - 3}
            width={6}
            height={6}
            style={{ fill: 'var(--zone-fg)' }}
          />
        )}

        {/* base */}
        <line
          className="chart-base"
          x1={PAD.left}
          x2={w - PAD.right}
          y1={PAD.top + plotH}
          y2={PAD.top + plotH}
          strokeWidth="1"
        />

        {/* horas */}
        {points.map((p, i) =>
          i % labelEvery === 0 ? (
            <text
              key={`x${i}`}
              className="chart-tick"
              x={x(i)}
              y={H - 9}
              textAnchor="middle"
              fontSize="11"
              style={{ fill: hover === i ? 'var(--zone-fg)' : 'var(--zone-deep)' }}
            >
              {p.label}
            </text>
          ) : null,
        )}
      </svg>
    </div>
  );
}

function niceStep(raw: number): number {
  const pow = Math.pow(10, Math.floor(Math.log10(Math.max(raw, 1))));
  const norm = raw / pow;
  const mult = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
  return mult * pow;
}

function formatTick(v: number): string {
  if (v >= 1000) return `${(v / 1000).toFixed(v % 1000 === 0 ? 0 : 1)}k`;
  return String(Math.round(v));
}
