import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useWidth } from '../hooks/useWidth';
import { Odometer } from './Odometer';
import { chaveStatus } from '../live';
import type { TransacaoDto } from '../types';

/**
 * 05 — CADÊNCIA
 *
 * O gráfico de fluxo da seção 02 conta transações por hora, e a hora é um
 * balde grande: ela esconde a rajada de doze segundos que costuma ser
 * justamente o que interessa. Esta seção não agrega nada — cada lançamento
 * da amostra é um traço, no instante em que aconteceu.
 *
 * A escova de seleção veio do padrão de "range navigator" de painel
 * financeiro. O que mudou foi a forma: nada de janela translúcida com
 * puxadores arredondados. A janela é uma faixa de papel limpo sobre uma
 * fita rebaixada, e os puxadores são duas barras de tinta cheia — peça de
 * régua, não controle de vídeo.
 *
 * Toda leitura à direita recorta com a janela. É o ponto da seção: o
 * operador escolhe o intervalo e o boletim responde só sobre ele.
 */

const ALTURA = 132;
const NAV = 38;
const TOPO = 12;

type Tom = 'verde' | 'amber' | 'signal' | 'azul';

const TOM: Record<string, Tom> = {
  aprovada: 'verde',
  suspeita: 'amber',
  bloqueada: 'signal',
  aceitaprovisoria: 'azul',
};

interface Traco {
  tx: TransacaoDto;
  t: number;
  tom: Tom;
}

/** Uma janela é sempre um par 0…1 sobre o eixo do tempo. */
interface Janela {
  a: number;
  b: number;
}

const CHEIA: Janela = { a: 0, b: 1 };

export function Cadence({ amostra, limiar = 0.7 }: { amostra: TransacaoDto[]; limiar?: number }) {
  const [ref, w] = useWidth(880);
  const [janela, setJanela] = useState<Janela>(CHEIA);
  const [pairado, setPairado] = useState<string | null>(null);
  const navRef = useRef<SVGSVGElement>(null);
  const arrasto = useRef<'a' | 'b' | 'corpo' | null>(null);
  const origem = useRef({ x: 0, a: 0, b: 0 });

  const { tracos, t0, t1 } = useMemo(() => {
    const linhas: Traco[] = amostra
      .map(tx => ({
        tx,
        t: new Date(tx.timestamp).getTime(),
        tom: TOM[chaveStatus(tx.status)] ?? 'azul',
      }))
      .filter(l => Number.isFinite(l.t))
      .sort((x, y) => x.t - y.t);

    const ini = linhas.length ? linhas[0].t : 0;
    const fim = linhas.length ? linhas[linhas.length - 1].t : 1;
    // Amostra de um instante só: sem intervalo não há eixo, e dividir por
    // zero jogaria todo mundo na mesma coluna.
    return { tracos: linhas, t0: ini, t1: fim > ini ? fim : ini + 1 };
  }, [amostra]);

  // A escala de altura é logarítmica porque a cauda de valores é longa: um
  // lançamento de dezenas de milhares achataria os outros noventa e nove
  // numa linha rente ao chão, e é entre eles que está a textura do fluxo.
  //
  // E o log é esticado entre o menor e o maior da amostra, não entre zero e
  // o maior. Ancorar no zero comprimiria tudo na metade de cima da fita e a
  // fita viraria um código de barras de altura uniforme — bonito e mudo.
  const alturaDe = useMemo(() => {
    const valores = tracos.map(l => l.tx.valor);
    const piso = Math.log10(Math.max(0, Math.min(...valores)) + 1);
    const topo = Math.log10(Math.max(1, ...valores) + 1);
    const faixa = topo - piso;
    // Piso de 7%: o menor lançamento ainda tem de existir na fita.
    return (v: number) => (faixa > 0 ? 0.07 + 0.93 * ((Math.log10(v + 1) - piso) / faixa) : 0.5);
  }, [tracos]);

  const posDe = useCallback((t: number) => (t - t0) / (t1 - t0), [t0, t1]);

  const dentro = useMemo(
    () =>
      tracos.filter(l => {
        const p = posDe(l.t);
        return p >= janela.a && p <= janela.b;
      }),
    [tracos, janela, posDe],
  );

  const leitura = useMemo(() => {
    const n = dentro.length;
    const valor = dentro.reduce((a, l) => a + l.tx.valor, 0);
    const bloq = dentro.filter(l => l.tom === 'signal').length;
    const escore = n ? dentro.reduce((a, l) => a + (l.tx.riskScore ?? 0), 0) / n : 0;
    const span = (t1 - t0) * (janela.b - janela.a);
    // Cadência: lançamentos por minuto dentro do recorte. Numa janela
    // apertada demais o número explode sem significar nada, então só sai
    // quando há pelo menos meio minuto de intervalo.
    const porMinuto = span > 30000 ? n / (span / 60000) : null;
    return { n, valor, taxa: n ? (bloq / n) * 100 : 0, escore: escore * 100, porMinuto };
  }, [dentro, janela, t0, t1]);

  // ── Escova ──────────────────────────────────────────────────────────
  const posDoEvento = useCallback((clientX: number) => {
    const cx = navRef.current?.getBoundingClientRect();
    if (!cx || cx.width === 0) return 0;
    return Math.min(1, Math.max(0, (clientX - cx.left) / cx.width));
  }, []);

  const pegar = (alvo: 'a' | 'b' | 'corpo') => (e: React.PointerEvent) => {
    e.preventDefault();
    e.stopPropagation();
    arrasto.current = alvo;
    origem.current = { x: posDoEvento(e.clientX), a: janela.a, b: janela.b };
  };

  // O ponteiro sai do SVG a toda hora num arrasto rápido; ouvir na janela é
  // o que impede o puxador de "cair" no meio do movimento.
  useEffect(() => {
    const mover = (e: PointerEvent) => {
      if (!arrasto.current) return;
      const p = posDoEvento(e.clientX);
      const MIN = 0.02;

      setJanela(j => {
        if (arrasto.current === 'a') return { a: Math.min(p, j.b - MIN), b: j.b };
        if (arrasto.current === 'b') return { a: j.a, b: Math.max(p, j.a + MIN) };
        // Corpo: desliza rígido, e encosta nas bordas sem encolher.
        const larg = origem.current.b - origem.current.a;
        const d = p - origem.current.x;
        const a = Math.min(1 - larg, Math.max(0, origem.current.a + d));
        return { a, b: a + larg };
      });
    };
    const soltar = () => {
      arrasto.current = null;
    };

    window.addEventListener('pointermove', mover);
    window.addEventListener('pointerup', soltar);
    window.addEventListener('pointercancel', soltar);
    return () => {
      window.removeEventListener('pointermove', mover);
      window.removeEventListener('pointerup', soltar);
      window.removeEventListener('pointercancel', soltar);
    };
  }, [posDoEvento]);

  /** Clique no trilho vazio recentra a janela ali, mantendo a largura. */
  const recentrar = (e: React.PointerEvent) => {
    const p = posDoEvento(e.clientX);
    const larg = janela.b - janela.a;
    const a = Math.min(1 - larg, Math.max(0, p - larg / 2));
    setJanela({ a, b: a + larg });
  };

  if (tracos.length === 0) {
    return (
      <section className="cad">
        <div className="cad-rot">
          <span className="idx">05</span>
          <span className="name">Cadência do fluxo</span>
        </div>
        <div className="painel-vazio">Sem amostra para desenhar a fita</div>
      </section>
    );
  }

  // Recuo de 5 px nas duas pontas: sem ele o primeiro e o último traço
  // caem metade fora da fita e viram um fio de meia espessura.
  const escalaX = (p: number) =>
    5 + ((p - janela.a) / (janela.b - janela.a)) * (w - 10);
  const foco = pairado ? tracos.find(l => l.tx.id === pairado) : null;
  const recortada = janela.a > 0 || janela.b < 1;

  return (
    <section className="cad">
      <div className="cad-rot">
        <span className="idx">05</span>
        <span className="name">Cadência do fluxo — lançamento a lançamento</span>
        <span className="cad-rot-nota">
          {recortada
            ? `recorte de ${dentro.length} de ${tracos.length}`
            : `${tracos.length} lançamentos`}
        </span>
        {recortada && (
          <button className="cad-limpar" onClick={() => setJanela(CHEIA)}>
            abrir tudo
          </button>
        )}
      </div>

      <div className="cad-corpo">
        {/* ── Fita ── */}
        <div className="cad-fita" ref={ref}>
          <svg
            className="cad-svg"
            viewBox={`0 0 ${w} ${ALTURA}`}
            preserveAspectRatio="none"
            onPointerLeave={() => setPairado(null)}
          >
            <line className="cad-piso" x1="0" y1={ALTURA - 0.5} x2={w} y2={ALTURA - 0.5} />

            {dentro.map((l, i) => {
              const x = escalaX(posDe(l.t));
              const h = alturaDe(l.tx.valor) * (ALTURA - TOPO);
              const alto = (l.tx.riskScore ?? 0) >= limiar;
              return (
                <line
                  key={l.tx.id}
                  className={`cad-traco cad-traco--${l.tom}${alto ? ' cad-traco--alto' : ''}${
                    pairado === l.tx.id ? ' on' : ''
                  }`}
                  x1={x}
                  y1={ALTURA}
                  x2={x}
                  y2={ALTURA - Math.max(2, h)}
                  style={{ animationDelay: `${Math.min(i, 40) * 9}ms` }}
                  onPointerEnter={() => setPairado(l.tx.id)}
                />
              );
            })}
          </svg>

          {foco && (
            <div
              className="cad-lupa"
              style={{ left: `${Math.min(86, Math.max(1, (escalaX(posDe(foco.t)) / w) * 100))}%` }}
            >
              <b>R$ {foco.tx.valor.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}</b>
              <span>{foco.tx.tipo}</span>
              <span>risco {((foco.tx.riskScore ?? 0) * 100).toFixed(0)}</span>
              <span>
                {new Date(foco.t).toLocaleTimeString('pt-BR', {
                  hour: '2-digit',
                  minute: '2-digit',
                  second: '2-digit',
                })}
              </span>
            </div>
          )}
        </div>

        {/* ── Leituras da janela ── */}
        <div className="cad-leituras">
          <div className="cad-leitura cad-leitura--forte">
            <span className="cad-leitura-lbl">Lançamentos no recorte</span>
            <b className="cad-leitura-num num">
              <Odometer value={leitura.n} />
            </b>
          </div>
          <div className="cad-leitura">
            <span className="cad-leitura-lbl">Valor somado</span>
            <b className="cad-leitura-val">
              <Odometer value={leitura.valor} decimals={2} prefix="R$ " />
            </b>
          </div>
          <div className="cad-leitura">
            <span className="cad-leitura-lbl">Taxa de bloqueio</span>
            <b className={`cad-leitura-val${leitura.taxa > 8 ? ' alerta' : ''}`}>
              <Odometer value={leitura.taxa} decimals={1} suffix="%" />
            </b>
          </div>
          <div className="cad-leitura">
            <span className="cad-leitura-lbl">Escore médio</span>
            <b className="cad-leitura-val">
              <Odometer value={leitura.escore} decimals={1} />
            </b>
          </div>
          <div className="cad-leitura">
            <span className="cad-leitura-lbl">Cadência</span>
            <b className="cad-leitura-val">
              {leitura.porMinuto === null ? (
                '——'
              ) : (
                <Odometer value={leitura.porMinuto} decimals={2} suffix=" /min" />
              )}
            </b>
          </div>
        </div>
      </div>

      {/* ── Escova de seleção ── */}
      <div className="cad-nav-wrap">
        <svg
          className="cad-nav"
          ref={navRef}
          viewBox={`0 0 1000 ${NAV}`}
          preserveAspectRatio="none"
          onPointerDown={recentrar}
        >
          <rect className="cad-nav-fundo" x="0" y="0" width="1000" height={NAV} />

          {tracos.map(l => {
            const x = posDe(l.t) * 1000;
            const h = alturaDe(l.tx.valor) * (NAV - 6);
            return (
              <line
                key={l.tx.id}
                className={`cad-nav-traco cad-nav-traco--${l.tom}`}
                x1={x}
                y1={NAV}
                x2={x}
                y2={NAV - Math.max(1.5, h)}
              />
            );
          })}

          {/* Fora da janela: papel rebaixado, não véu escuro. */}
          <rect className="cad-nav-fora" x="0" y="0" width={janela.a * 1000} height={NAV} />
          <rect
            className="cad-nav-fora"
            x={janela.b * 1000}
            y="0"
            width={(1 - janela.b) * 1000}
            height={NAV}
          />

          <rect
            className="cad-nav-corpo"
            x={janela.a * 1000}
            y="0"
            width={(janela.b - janela.a) * 1000}
            height={NAV}
            onPointerDown={pegar('corpo')}
          />
          <rect
            className="cad-nav-pega"
            x={janela.a * 1000 - 4}
            y="0"
            width="8"
            height={NAV}
            onPointerDown={pegar('a')}
          />
          <rect
            className="cad-nav-pega"
            x={janela.b * 1000 - 4}
            y="0"
            width="8"
            height={NAV}
            onPointerDown={pegar('b')}
          />
        </svg>

        <div className="cad-eixo">
          <span>{marca(t0 + janela.a * (t1 - t0))}</span>
          <span className="cad-eixo-meio">arraste a janela · clique no trilho para recentrar</span>
          <span>{marca(t0 + janela.b * (t1 - t0))}</span>
        </div>
      </div>
    </section>
  );
}

function marca(t: number) {
  return new Date(t).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}
