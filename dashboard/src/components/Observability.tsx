import { useEffect, useState } from 'react';
import { Gauge } from './Gauge';
import { Sparkline } from './Sparkline';
import { Odometer } from './Odometer';
import { useTelemetria } from '../telemetry';

/**
 * 05 — OBSERVABILIDADE
 *
 * Este painel não descreve o serviço em produção: descreve **este enlace**,
 * do navegador até a API, medido nesta sessão. Cada latência aqui saiu de
 * um `performance.now()` em volta de uma chamada real; a fita de
 * disponibilidade é montada a partir dos desfechos dessas chamadas; a
 * vazão conta eventos que o hub efetivamente entregou. Nada é estimado.
 *
 * A honestidade importa mais que o número bonito — um painel de
 * observabilidade que inventa dados é pior que não ter painel.
 */

const ESTADO_TXT: Record<string, string> = {
  nominal: 'nominal',
  lento: 'lento',
  degradado: 'degradado',
  critico: 'crítico',
};

/**
 * Acima de um segundo, milissegundos param de ser legíveis — "7.537 ms"
 * exige que o leitor conte casas. Vira "7,54 s".
 */
function duracao(ms: number): { valor: number; casas: number; unidade: string } {
  return ms >= 1000
    ? { valor: ms / 1000, casas: 2, unidade: 's' }
    : { valor: Math.round(ms), casas: 0, unidade: 'ms' };
}

function curta(ms: number): string {
  const d = duracao(ms);
  return `${d.valor.toLocaleString('pt-BR', {
    minimumFractionDigits: d.casas,
    maximumFractionDigits: d.casas,
  })} ${d.unidade}`;
}

function relogio(segundos: number): string {
  const hh = String(Math.floor(segundos / 3600)).padStart(2, '0');
  const mm = String(Math.floor((segundos % 3600) / 60)).padStart(2, '0');
  const ss = String(segundos % 60).padStart(2, '0');
  return `${hh}:${mm}:${ss}`;
}

/** Tempo desde a primeira pintura desta sessão. */
function useDuracaoSessao(): string {
  const [texto, setTexto] = useState(() => relogio(0));

  useEffect(() => {
    const inicio = Date.now();
    const t = setInterval(
      () => setTexto(relogio(Math.floor((Date.now() - inicio) / 1000))),
      1000,
    );
    return () => clearInterval(t);
  }, []);

  return texto;
}

export function Observability({ conectado }: { conectado: boolean }) {
  const t = useTelemetria();
  const sessao = useDuracaoSessao();

  const piorRota = t.rotas[0];

  return (
    <div className="obs">
      {/* ── Disponibilidade + fita ── */}
      <div className="obs-cel obs-cel--fita">
        <div className="obs-rot">
          Disponibilidade do enlace
          <em className={`obs-estado obs-estado--${t.saude}`}>{ESTADO_TXT[t.saude]}</em>
        </div>

        <div className="obs-num obs-num--grande">
          <Odometer value={t.disponibilidade} decimals={2} suffix="%" />
        </div>

        <div className="fita" role="img" aria-label={`Últimos ${t.fatias.length} intervalos de 5 segundos`}>
          {t.fatias.map((f, i) => (
            <i key={i} className={`fita-slot fita-slot--${f}`} style={{ animationDelay: `${i * 11}ms` }} />
          ))}
        </div>

        <div className="obs-sub">
          <span>240 s · fatias de 5 s</span>
          <span>{t.amostras} amostras</span>
          <span className={t.falhas > 0 ? 'ruim' : ''}>{t.falhas} falhas</span>
        </div>
      </div>

      {/* ── Orçamento de erro ── */}
      <div className="obs-cel obs-cel--gauge">
        <div className="obs-rot">Orçamento de erro</div>
        <Gauge
          fracao={t.orcamento / 100}
          leitura={`${t.orcamento.toFixed(0)}%`}
          legenda="consumido · alvo 99,50%"
          zona={0.7}
          tom={t.orcamento > 70 ? 'signal' : t.orcamento > 30 ? 'amber' : 'verde'}
        />
      </div>

      {/* ── Pulso: vazão, quadros, sessão ── */}
      <div className="obs-cel obs-cel--pulso">
        <div className="obs-rot">Pulso</div>

        <div className="pulso-linha">
          <div className="pulso-num">
            <Odometer value={t.vazao} />
            <span>eventos/min</span>
          </div>
          <span className={`led${conectado ? ' led--on' : ''}`} />
        </div>

        <div className="pulso-linha">
          <div className="pulso-num pulso-num--menor">
            {t.quadros === null ? <span className="pulso-mudo">——</span> : <Odometer value={t.quadros} />}
            <span>quadros/s</span>
          </div>
          <Sparkline serie={t.quadrosSerie} w={92} h={26} destaque="min" tom="var(--zone-dim)" />
        </div>

        <div className="obs-sub">
          <span>sessão {sessao}</span>
          <span>{t.eventosTotal} eventos</span>
        </div>
      </div>

      {/* ── Latência ── */}
      <div className="obs-cel obs-cel--lat">
        <div className="obs-rot">
          Latência de resposta
          <em className="obs-rot-nota">percentis sobre as últimas {t.amostras} chamadas</em>
        </div>

        <div className="percentis">
          {[
            { k: 'p50', v: t.p50 },
            { k: 'p90', v: t.p90 },
            { k: 'p99', v: t.p99 },
          ].map(p => {
            const d = duracao(p.v);
            return (
              <div className="percentil" key={p.k}>
                <span className="percentil-k">{p.k}</span>
                <span className="percentil-v">
                  <Odometer value={d.valor} decimals={d.casas} />
                  <em>{d.unidade}</em>
                </span>
              </div>
            );
          })}

          <div className="percentil percentil--traco">
            <Sparkline serie={t.serie} w={220} h={44} tom="var(--zone-fg)" />
            <span className="percentil-k">
              pior {curta(t.pior)} · última {curta(t.ultima)}
            </span>
          </div>
        </div>
      </div>

      {/* ── Rotas ── */}
      <div className="obs-cel obs-cel--rotas">
        <div className="obs-rot">
          Rotas
          {piorRota && <em className="obs-rot-nota">mais lenta: {piorRota.rota}</em>}
        </div>

        <div className="rotas">
          {t.rotas.length === 0 && <div className="painel-vazio painel-vazio--zona">Nenhuma chamada registrada</div>}
          {t.rotas.map((r, i) => {
            const teto = Math.max(...t.rotas.map(x => x.p50), 1);
            const d = duracao(r.p50);
            return (
              <div className="rota" key={r.rota} style={{ ['--i' as string]: i }}>
                <span className="rota-nome">{r.rota}</span>
                <span className="rota-barra">
                  <i style={{ width: `${(r.p50 / teto) * 100}%`, animationDelay: `${i * 70}ms` }} />
                </span>
                <span className="rota-ms">
                  {d.valor.toLocaleString('pt-BR', {
                    minimumFractionDigits: d.casas,
                    maximumFractionDigits: d.casas,
                  })}
                  <em>{d.unidade}</em>
                </span>
                <span className="rota-n">{r.n}×</span>
                <span className={`rota-falhas${r.falhas > 0 ? ' ruim' : ''}`}>
                  {r.falhas > 0 ? `${r.falhas} falha${r.falhas > 1 ? 's' : ''}` : '——'}
                </span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
