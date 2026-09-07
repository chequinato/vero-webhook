import { useMemo, useState } from 'react';
import { Donut, type Fatia } from './Donut';
import { RiskHistogram } from './RiskHistogram';
import { Threshold } from './Threshold';
import { Matrix } from './Matrix';
import { Odometer } from './Odometer';
import type { Stats, TransacaoDto } from '../types';

/**
 * 04 — COMPOSIÇÃO
 *
 * Três leituras da mesma amostra, do geral ao específico: o anel diz *o
 * que* o motor decidiu, a distribuição diz *com que folga* decidiu, e a
 * matriz diz *onde* o risco se concentra. Nesta ordem, e nesta página, é
 * uma linha de raciocínio — não três widgets lado a lado.
 *
 * A amostra é o corte máximo que a API entrega numa chamada (100 linhas);
 * o rodapé diz isso em voz alta, porque um painel que esconde o tamanho
 * da amostra está mentindo por omissão.
 */

export function Composition({ stats, amostra }: { stats: Stats | null; amostra: TransacaoDto[] }) {
  // O limiar simulado é estado desta seção, não de um dos gráficos: a
  // distribuição e a curva de operação são duas leituras do mesmo corte, e
  // arrastar numa tem de mover a outra.
  const [limiar, setLimiar] = useState(70);

  const fatias: Fatia[] = useMemo(
    () =>
      stats
        ? [
            { key: 'apr', label: 'Aprovadas', value: stats.aprovadas, cor: 'var(--verde)' },
            { key: 'sus', label: 'Suspeitas', value: stats.suspeitas, cor: 'var(--amber)' },
            { key: 'blq', label: 'Bloqueadas', value: stats.bloqueadas, cor: 'var(--signal)' },
            { key: 'pnd', label: 'Em análise', value: stats.aceitasProvisoria, cor: 'var(--azul)' },
          ].filter(f => f.value > 0)
        : [],
    [stats],
  );

  const derivadas = useMemo(() => {
    if (amostra.length === 0) return null;

    const valores = amostra.map(t => t.valor).sort((a, b) => a - b);
    const soma = valores.reduce((a, v) => a + v, 0);
    const mediana = valores[Math.floor(valores.length / 2)];

    // Concentração: fatia do valor total que cabe nos 10% maiores lançamentos.
    const corte = Math.max(1, Math.round(valores.length * 0.1));
    const topo = valores.slice(-corte).reduce((a, v) => a + v, 0);
    const concentracao = soma > 0 ? (topo / soma) * 100 : 0;

    const bloqueadas = amostra.filter(t => t.status === 'bloqueada').length;
    const aprovadas = amostra.filter(t => t.status === 'aprovada').length;
    const escores = amostra.map(t => t.riskScore ?? 0);
    const media = escores.reduce((a, v) => a + v, 0) / escores.length;
    const desvio = Math.sqrt(escores.reduce((a, v) => a + (v - media) ** 2, 0) / escores.length);

    return {
      mediana,
      concentracao,
      razao: aprovadas > 0 ? bloqueadas / aprovadas : 0,
      desvio: desvio * 100,
    };
  }, [amostra]);

  return (
    <section className="comp">
      <div className="comp-topo">
        {/* ── Anel ── */}
        <div className="comp-painel comp-painel--anel">
          <div className="comp-rot">
            <span className="idx">04</span>
            <span className="name">Composição das decisões</span>
          </div>

          {fatias.length > 0 ? (
            <Donut fatias={fatias} total={stats?.total ?? 0} />
          ) : (
            <div className="painel-vazio">Sem decisões registradas</div>
          )}

          {derivadas && (
            <div className="derivadas">
              <div className="derivada">
                <span>Mediana do lançamento</span>
                <b><Odometer value={derivadas.mediana} decimals={2} prefix="R$ " /></b>
              </div>
              <div className="derivada">
                <span>Concentração nos 10% maiores</span>
                <b><Odometer value={derivadas.concentracao} decimals={1} suffix="%" /></b>
              </div>
              <div className="derivada">
                <span>Razão bloqueio / aprovação</span>
                <b><Odometer value={derivadas.razao} decimals={3} /></b>
              </div>
              <div className="derivada">
                <span>Dispersão do escore (σ)</span>
                <b><Odometer value={derivadas.desvio} decimals={1} suffix=" p.p." /></b>
              </div>
            </div>
          )}
        </div>

        {/* ── Distribuição ── */}
        <div className="comp-painel comp-painel--dist">
          <div className="comp-rot">
            <span className="idx">04.b</span>
            <span className="name">Distribuição do escore de risco</span>
          </div>
          <RiskHistogram amostra={amostra} limiar={limiar} />

          {amostra.length > 0 && (
            <div className="comp-curva">
              <Threshold amostra={amostra} limiar={limiar} onLimiar={setLimiar} />
            </div>
          )}
        </div>
      </div>

      {/* ── Matriz ── */}
      <div className="comp-painel comp-painel--matriz">
        <div className="comp-rot">
          <span className="idx">04.c</span>
          <span className="name">Matriz tipo de lançamento × faixa de risco</span>
          <span className="comp-rot-nota">amostra de {amostra.length} linhas</span>
        </div>
        <Matrix amostra={amostra} />
      </div>
    </section>
  );
}
