import { useMemo, useRef, useState, type Dispatch, type SetStateAction } from 'react';
import { useWidth } from '../hooks/useWidth';
import { Odometer } from './Odometer';
import type { TransacaoDto } from '../types';

/**
 * 04.b — CURVA DE OPERAÇÃO
 *
 * O histograma logo acima mostra onde a massa está; esta curva mostra o que
 * acontece se o corte se mexer. Mesmo eixo horizontal, de propósito: as duas
 * peças são a mesma pergunta lida de dois jeitos, e alinhá-las deixa o leitor
 * traçar a vertical com o olho.
 *
 * Duas curvas, porque contagem e dinheiro não andam juntos: a fração de
 * *linhas* retidas cai rápido, a fração de *valor* retido costuma cair devagar
 * — poucos lançamentos grandes carregam a maior parte da exposição. O vão
 * entre as duas curvas é o argumento inteiro para mexer no limiar.
 *
 * Nada aqui é previsão. É contagem direta sobre a amostra: para cada corte,
 * quantas linhas e quanto valor ficariam acima dele.
 */

const H = 178;
const PAD = { top: 16, right: 40, bottom: 26, left: 34 };

/** Corte em produção — o mesmo do `DashboardController`. */
const CORTE_VIGENTE = 70;

const brl = (v: number) =>
  v.toLocaleString('pt-BR', { maximumFractionDigits: 0 });

export function Threshold({
  amostra,
  limiar,
  onLimiar,
}: {
  amostra: TransacaoDto[];
  limiar: number;
  /** Aceita atualizador para a tecla repetida não perder passos entre pinturas. */
  onLimiar: Dispatch<SetStateAction<number>>;
}) {
  const [ref, w] = useWidth(520);
  const svgRef = useRef<SVGSVGElement>(null);
  const [arrastando, setArrastando] = useState(false);

  /**
   * Para cada corte inteiro de 0 a 100, quantas linhas e quanto valor ficam
   * acima dele. Uma varredura só sobre a amostra ordenada, não cem.
   */
  const { linhas, valores, nTotal, valorTotal } = useMemo(() => {
    const pontos = amostra
      .filter(t => t.riskScore !== null && t.riskScore !== undefined)
      .map(t => ({ s: Math.round((t.riskScore as number) * 100), v: t.valor }));

    const porCorte = new Array<number>(101).fill(0);
    const valorPorCorte = new Array<number>(101).fill(0);
    for (const p of pontos) {
      const i = Math.min(100, Math.max(0, p.s));
      porCorte[i] += 1;
      valorPorCorte[i] += p.v;
    }

    // Soma de sufixo: acumulado de trás para frente = "quantos ≥ corte".
    const linhas = new Array<number>(101).fill(0);
    const valores = new Array<number>(101).fill(0);
    let accN = 0;
    let accV = 0;
    for (let i = 100; i >= 0; i--) {
      accN += porCorte[i];
      accV += valorPorCorte[i];
      linhas[i] = accN;
      valores[i] = accV;
    }

    return { linhas, valores, nTotal: linhas[0], valorTotal: valores[0] };
  }, [amostra]);

  if (nTotal === 0) {
    return <div className="painel-vazio">Sem amostra para simular cortes</div>;
  }

  const plotW = w - PAD.left - PAD.right;
  const plotH = H - PAD.top - PAD.bottom;

  const x = (t: number) => PAD.left + (t / 100) * plotW;
  const y = (frac: number) => PAD.top + plotH - frac * plotH;

  const caminho = (serie: number[], teto: number) =>
    serie
      .map((v, t) => `${t === 0 ? 'M' : 'L'}${x(t).toFixed(1)} ${y(teto > 0 ? v / teto : 0).toFixed(1)}`)
      .join(' ');

  const dLinhas = caminho(linhas, nTotal);
  const dValores = caminho(valores, valorTotal);

  // Faixa entre as duas curvas: desce pela de valor, volta pela de contagem.
  const dVao = [
    dValores,
    ...linhas
      .map((v, t) => ({ t, v }))
      .reverse()
      .map(({ t, v }) => `L${x(t).toFixed(1)} ${y(nTotal > 0 ? v / nTotal : 0).toFixed(1)}`),
    'Z',
  ].join(' ');

  const corte = Math.round(Math.min(100, Math.max(0, limiar)));
  const retidas = linhas[corte];
  const valorRetido = valores[corte];
  const deltaLinhas = retidas - linhas[CORTE_VIGENTE];
  const deltaValor = valorRetido - valores[CORTE_VIGENTE];

  const mover = (clientX: number) => {
    const svg = svgRef.current;
    if (!svg) return;
    const r = svg.getBoundingClientRect();
    const escala = w / r.width || 1;
    const px = (clientX - r.left) * escala;
    const t = ((px - PAD.left) / plotW) * 100;
    onLimiar(Math.round(Math.min(100, Math.max(0, t))));
  };

  return (
    <div className="curva" ref={ref}>
      <div className="curva-topo">
        <div className="curva-rot">
          <span className="idx">04.b·ii</span>
          <span className="name">Curva de operação — e se o corte fosse outro</span>
        </div>

        <div className="curva-leituras">
          <div className="curva-leitura">
            <span className="curva-k">corte</span>
            <span className="curva-v curva-v--corte">
              <Odometer value={corte} suffix="%" />
            </span>
          </div>
          <div className="curva-leitura">
            <span className="curva-k">retidas</span>
            <span className="curva-v">
              <Odometer value={retidas} />
              <em>de {nTotal}</em>
            </span>
          </div>
          <div className="curva-leitura">
            <span className="curva-k">valor retido</span>
            <span className="curva-v">
              <Odometer value={Math.round(valorRetido)} prefix="R$ " />
            </span>
          </div>
        </div>
      </div>

      <svg
        ref={svgRef}
        viewBox={`0 0 ${w} ${H}`}
        width={w}
        height={H}
        className={arrastando ? 'curva-svg curva-svg--arrasto' : 'curva-svg'}
        role="slider"
        tabIndex={0}
        aria-label="Limiar de bloqueio simulado"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={corte}
        onPointerDown={e => {
          e.currentTarget.setPointerCapture(e.pointerId);
          setArrastando(true);
          mover(e.clientX);
        }}
        onPointerMove={e => {
          if (arrastando) mover(e.clientX);
        }}
        onPointerUp={e => {
          e.currentTarget.releasePointerCapture(e.pointerId);
          setArrastando(false);
        }}
        onKeyDown={e => {
          const passo = e.shiftKey ? 10 : 1;
          if (e.key === 'ArrowLeft') { onLimiar(v => Math.max(0, v - passo)); e.preventDefault(); }
          if (e.key === 'ArrowRight') { onLimiar(v => Math.min(100, v + passo)); e.preventDefault(); }
        }}
      >
        {/* grade */}
        {[0, 0.5, 1].map(k => (
          <line key={k} className="curva-grid" x1={PAD.left} x2={w - PAD.right} y1={y(k)} y2={y(k)} />
        ))}

        {/* área entre as duas curvas: o vão é a mensagem */}
        <path className="curva-vao" d={dVao} />

        <path className="curva-linha curva-linha--valor" d={dValores} pathLength={100} />
        <path className="curva-linha curva-linha--conta" d={dLinhas} pathLength={100} />

        {/* corte vigente, impresso e imóvel */}
        <g className="curva-vigente">
          <line x1={x(CORTE_VIGENTE)} x2={x(CORTE_VIGENTE)} y1={PAD.top - 6} y2={PAD.top + plotH} />
          <text x={x(CORTE_VIGENTE) - 5} y={PAD.top + 2} textAnchor="end" fontSize="10">
            vigente 70
          </text>
        </g>

        {/* cursor arrastável */}
        <g className="curva-cursor">
          <line x1={x(corte)} x2={x(corte)} y1={PAD.top - 10} y2={PAD.top + plotH} />
          <rect x={x(corte) - 4} y={y(retidas / nTotal) - 4} width={8} height={8} />
          <rect
            className="curva-cursor-valor"
            x={x(corte) - 3}
            y={y(valorTotal > 0 ? valorRetido / valorTotal : 0) - 3}
            width={6}
            height={6}
          />
          <path d={`M${x(corte) - 5} ${PAD.top - 10} L${x(corte) + 5} ${PAD.top - 10} L${x(corte)} ${PAD.top - 3} Z`} />
        </g>

        <line className="curva-base" x1={PAD.left} x2={w - PAD.right} y1={PAD.top + plotH} y2={PAD.top + plotH} />

        {[0, 25, 50, 75, 100].map(p => (
          <text
            key={p}
            className="curva-tick"
            x={x(p)}
            y={H - 8}
            textAnchor={p === 0 ? 'start' : p === 100 ? 'end' : 'middle'}
            fontSize="10"
          >
            {p}%
          </text>
        ))}
        <text className="curva-tick" x={PAD.left - 7} y={y(1) + 4} textAnchor="end" fontSize="10">100</text>
        <text className="curva-tick" x={PAD.left - 7} y={y(0) + 4} textAnchor="end" fontSize="10">0</text>
      </svg>

      <div className="curva-pe">
        <span className="curva-legenda">
          <i className="curva-amostra-conta" /> linhas retidas
          <i className="curva-amostra-valor" /> valor retido
        </span>

        <span className={`curva-delta${corte === CORTE_VIGENTE ? ' curva-delta--nulo' : ''}`}>
          {corte === CORTE_VIGENTE ? (
            'no corte vigente'
          ) : (
            <>
              {deltaLinhas >= 0 ? '+' : '−'}
              <b>{Math.abs(deltaLinhas)}</b> linha{Math.abs(deltaLinhas) === 1 ? '' : 's'} ·{' '}
              {deltaValor >= 0 ? '+' : '−'}
              <b>R$ {brl(Math.abs(deltaValor))}</b> em relação ao corte vigente
            </>
          )}
        </span>
      </div>

      <p className="serif-note curva-nota">
        Arraste o cursor sobre a curva. Descer o corte retém mais valor e prende mais
        cliente legítimo; subir devolve fluidez e deixa exposição passar. A distância
        entre as duas linhas diz quanto dessa exposição está concentrada em poucos
        lançamentos — quando elas se afastam, mover o limiar custa pouco atrito e
        segura muito dinheiro.
      </p>
    </div>
  );
}
