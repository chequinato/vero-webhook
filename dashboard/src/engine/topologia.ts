import type { TransacaoDto } from '../types';
import { chaveStatus } from '../live';

/**
 * TOPOLOGIA DO MOTOR
 *
 * A malha não é enfeite generativo: cada nó é um estágio real do pipeline
 * do Vero, na ordem em que uma transação os atravessa.
 *
 *   barramento → regras síncronas → modelo FastTree → regras assíncronas → decisão
 *
 * Os nomes vêm do backend: as duas regras síncronas são `ValorAltoRule` e
 * `ScoreBaixoRule`; as sete features do meio são exatamente as que
 * `MlRiskScoringService.ExtrairFeatures` monta; as três assíncronas são
 * `VelocityRule`, `HorarioEstranhoRule` e `ValorRedondoRule`.
 *
 * ── Sobre honestidade ──
 * O **desfecho** e o **escore** de cada partícula são reais: vêm da API.
 * O **trajeto** pelo miolo é ilustrativo — a API não devolve atribuição por
 * feature, então o roteamento é derivado dos campos que temos (valor, hora,
 * motivo, escore) de forma determinística. A legenda da cena diz isso em voz
 * alta; um gráfico que finge saber o que o modelo pensou seria pior que não
 * ter gráfico.
 */

export interface No {
  id: string;
  lbl: string;
  estagio: number;
  x: number;
  y: number;
  z: number;
  /** Nós de decisão carregam a chave de status para filtrar o registro. */
  status?: string;
}

export interface Estagio {
  id: string;
  rotulo: string;
  x: number;
}

const TAU = Math.PI * 2;

export const ESTAGIOS: Estagio[] = [
  { id: 'entrada', rotulo: 'Barramento', x: -27 },
  { id: 'sinc', rotulo: 'Regras síncronas', x: -14 },
  { id: 'modelo', rotulo: 'Modelo FastTree', x: 0 },
  { id: 'assinc', rotulo: 'Regras assíncronas', x: 14 },
  { id: 'decisao', rotulo: 'Decisão', x: 27 },
];

/** Anel de nós num plano YZ — a silhueta estreita, incha no meio, estreita. */
function anel(
  estagio: number,
  raio: number,
  itens: Array<{ id: string; lbl: string }>,
): No[] {
  return itens.map((n, j) => {
    const a = (j / itens.length) * TAU;
    return {
      ...n,
      estagio,
      x: ESTAGIOS[estagio].x,
      y: raio * Math.cos(a),
      z: raio * Math.sin(a),
    };
  });
}

/** A saída não é um anel: é uma coluna, como um registrador. */
function coluna(estagio: number, itens: Array<{ id: string; lbl: string; status: string }>): No[] {
  const passo = 5.2;
  const topo = ((itens.length - 1) * passo) / 2;
  return itens.map((n, j) => ({
    ...n,
    estagio,
    x: ESTAGIOS[estagio].x,
    y: topo - j * passo,
    z: 0,
  }));
}

export const NOS: No[] = [
  ...anel(0, 0, [{ id: 'entrada', lbl: 'entrada' }]),

  ...anel(1, 5.4, [
    { id: 'valor_alto', lbl: 'valor alto' },
    { id: 'score_baixo', lbl: 'score baixo' },
  ]),

  ...anel(2, 9.6, [
    { id: 'Valor', lbl: 'valor' },
    { id: 'HoraDoDia', lbl: 'hora do dia' },
    { id: 'ScoreRemetente', lbl: 'score remetente' },
    { id: 'TransacoesRecentes', lbl: 'transações recentes' },
    { id: 'IsValorRedondo', lbl: 'valor redondo' },
    { id: 'IsHorarioEstranho', lbl: 'horário estranho' },
    { id: 'RazaoValorScore', lbl: 'razão valor/score' },
  ]),

  ...anel(3, 6.4, [
    { id: 'velocity', lbl: 'velocidade' },
    { id: 'horario', lbl: 'horário estranho' },
    { id: 'redondo', lbl: 'valor redondo' },
  ]),

  ...coluna(4, [
    { id: 'aprovada', lbl: 'aprovada', status: 'aprovada' },
    { id: 'aceitaprovisoria', lbl: 'em análise', status: 'aceitaprovisoria' },
    { id: 'suspeita', lbl: 'suspeita', status: 'suspeita' },
    { id: 'bloqueada', lbl: 'bloqueada', status: 'bloqueada' },
  ]),
];

const porEstagio = (e: number) =>
  NOS.map((n, i) => ({ n, i })).filter(({ n }) => n.estagio === e);

export const INDICE: Record<string, number> = Object.fromEntries(
  NOS.map((n, i) => [n.id, i]),
);

/** Arestas: malha completa entre estágios vizinhos, mais o aro de cada anel. */
export const ARESTAS: Array<[number, number]> = (() => {
  const out: Array<[number, number]> = [];

  for (let e = 0; e < ESTAGIOS.length - 1; e++) {
    for (const a of porEstagio(e)) {
      for (const b of porEstagio(e + 1)) out.push([a.i, b.i]);
    }
  }

  // Aro: fecha o anel de cada estágio intermediário; a coluna de decisão
  // é costurada de cima a baixo, como uma régua.
  for (let e = 1; e < ESTAGIOS.length; e++) {
    const grupo = porEstagio(e);
    if (grupo.length < 2) continue;
    const fecha = e < ESTAGIOS.length - 1;
    for (let j = 0; j < grupo.length - (fecha ? 0 : 1); j++) {
      out.push([grupo[j].i, grupo[(j + 1) % grupo.length].i]);
    }
  }

  return out;
})();

// ══════════════════════════════════════════════════════════════════════
// ROTEAMENTO
// ══════════════════════════════════════════════════════════════════════

/** Espalhamento determinístico a partir do id — nunca `Math.random`. */
function digestao(id: string): number {
  let h = 2166136261;
  for (let i = 0; i < id.length; i++) {
    h ^= id.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return (h >>> 0) / 4294967296;
}

const contem = (motivo: string | null, ...termos: string[]) =>
  !!motivo && termos.some(t => motivo.toLowerCase().includes(t));

/**
 * Escolhe por qual nó de cada estágio a transação passa. Determinístico:
 * a mesma transação desenha sempre o mesmo caminho, o que importa quando
 * o analista clica nela e espera reconhecer o traçado.
 */
export function rotear(tx: TransacaoDto): number[] {
  const hora = new Date(tx.timestamp).getHours();
  const risco = tx.riskScore ?? 0;
  const valor = tx.valor;
  const redondo = valor >= 1000 && valor % 1000 === 0;
  const horaEstranha = hora >= 1 && hora <= 5;
  const d = digestao(tx.id);

  const sinc = valor >= 50000 || contem(tx.motivo, 'limite', 'valor')
    ? 'valor_alto'
    : 'score_baixo';

  const modelo =
    horaEstranha ? 'IsHorarioEstranho'
      : redondo ? 'IsValorRedondo'
      : valor > 20000 ? 'Valor'
      : risco > 0.7 ? 'RazaoValorScore'
      : contem(tx.motivo, 'velocidade') ? 'TransacoesRecentes'
      : contem(tx.motivo, 'perfil', 'destinatário') ? 'ScoreRemetente'
      : d < 0.34 ? 'HoraDoDia'
      : d < 0.67 ? 'ScoreRemetente'
      : 'TransacoesRecentes';

  const assinc =
    contem(tx.motivo, 'velocidade') ? 'velocity'
      : horaEstranha || contem(tx.motivo, 'horário') ? 'horario'
      : redondo ? 'redondo'
      : d < 0.5 ? 'velocity'
      : 'horario';

  const chave = chaveStatus(tx.status);
  const decisao = INDICE[chave] !== undefined ? chave : 'aceitaprovisoria';

  return [INDICE.entrada, INDICE[sinc], INDICE[modelo], INDICE[assinc], INDICE[decisao]];
}

/** Chave de status do último nó de uma rota. */
export function estadoDaRota(rota: number[]): string {
  return NOS[rota[rota.length - 1]].status ?? 'aceitaprovisoria';
}
