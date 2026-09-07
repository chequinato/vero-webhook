/**
 * Traço fino de série curta. Sem eixos, sem grade: a escala está no
 * texto ao lado. Marca o pior ponto com um quadrado — é o único ponto
 * que alguém procura numa série de latência.
 */

interface Props {
  serie: number[];
  w?: number;
  h?: number;
  /** Marca o máximo (latência) ou o mínimo (quadros por segundo). */
  destaque?: 'max' | 'min';
  tom?: string;
}

export function Sparkline({ serie, w = 180, h = 38, destaque = 'max', tom = 'currentColor' }: Props) {
  if (serie.length < 2) {
    return <div className="spark spark--vazio" style={{ width: w, height: h }} />;
  }

  const min = Math.min(...serie);
  const max = Math.max(...serie);
  const span = max - min || 1;
  const px = (i: number) => (i / (serie.length - 1)) * w;
  const py = (v: number) => h - 2 - ((v - min) / span) * (h - 4);

  const d = serie.map((v, i) => `${i === 0 ? 'M' : 'L'}${px(i).toFixed(1)} ${py(v).toFixed(1)}`).join(' ');
  const alvo = destaque === 'max' ? max : min;
  const iAlvo = serie.indexOf(alvo);

  return (
    <svg className="spark" viewBox={`0 0 ${w} ${h}`} width={w} height={h} aria-hidden="true">
      <line className="spark-base" x1={0} x2={w} y1={h - 1} y2={h - 1} />
      <path className="spark-linha" d={d} pathLength={100} style={{ stroke: tom }} />
      <rect
        className="spark-pico"
        x={px(iAlvo) - 2.5}
        y={py(alvo) - 2.5}
        width={5}
        height={5}
        style={{ fill: tom }}
      />
    </svg>
  );
}
