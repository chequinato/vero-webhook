import type { Stats, VolumeHora, TransacaoDto } from './types';

/**
 * APLICAÇÃO LOCAL DE EVENTOS
 *
 * O painel não espera a próxima busca para se mexer. Quando o hub entrega uma
 * transação, estas funções recalculam os agregados no mesmo quadro — o
 * contador rola, a coluna da hora cresce, o anel se redistribui — e a busca no
 * servidor vem depois só para reconciliar. Sem isto, o dashboard de um sistema
 * "ao vivo" ficaria parado até dez segundos, que é exatamente a sensação de
 * coisa morta que se quer evitar.
 *
 * Tudo aqui é puro e imutável: as funções devolvem estado novo e nunca mexem
 * no que receberam, porque o React precisa da identidade trocada para
 * repintar, e o contador mecânico precisa comparar valor antigo com novo.
 */

/**
 * A API serializa o enum como `aceitaprovisoria`; o modo demonstração usa
 * `aceita_provisoria`. Normaliza os dois para uma chave só.
 */
export function chaveStatus(status: string): string {
  return status.replace(/_/g, '').toLowerCase();
}

type CampoContagem = 'aprovadas' | 'bloqueadas' | 'suspeitas' | 'aceitasProvisoria';

const CAMPO: Record<string, CampoContagem | undefined> = {
  aprovada: 'aprovadas',
  bloqueada: 'bloqueadas',
  suspeita: 'suspeitas',
  aceitaprovisoria: 'aceitasProvisoria',
};

/** Soma uma transação recém-chegada aos agregados. */
export function somaTransacao(stats: Stats, tx: TransacaoDto): Stats {
  const campo = CAMPO[chaveStatus(tx.status)];
  const total = stats.total + 1;
  const valorTotal = stats.valorTotal + tx.valor;

  const proximo: Stats = {
    ...stats,
    total,
    valorTotal,
    valorMedio: valorTotal / total,
    // Média corrida: reponderar sem guardar a soma inteira dos escores.
    riskScoreMedio: (stats.riskScoreMedio * stats.total + (tx.riskScore ?? 0)) / total,
  };

  // Status desconhecido entra no total mas não em nenhuma coluna; a próxima
  // reconciliação com o servidor acerta a distribuição.
  if (campo) proximo[campo] = stats[campo] + 1;

  return proximo;
}

/**
 * Move uma transação de um estado para outro. O total não muda — só a
 * distribuição e a média de risco, que absorve a diferença entre o escore
 * antigo e o novo diluída no tamanho da janela.
 */
export function trocaStatus(
  stats: Stats,
  de: string,
  para: string,
  scoreAnterior: number | null,
  scoreNovo: number | null,
): Stats {
  const campoDe = CAMPO[chaveStatus(de)];
  const campoPara = CAMPO[chaveStatus(para)];
  if (!campoDe || !campoPara || campoDe === campoPara) return stats;

  const proximo: Stats = { ...stats };
  proximo[campoDe] = Math.max(0, stats[campoDe] - 1);
  proximo[campoPara] = stats[campoPara] + 1;

  if (scoreNovo !== null && stats.total > 0) {
    proximo.riskScoreMedio =
      stats.riskScoreMedio + (scoreNovo - (scoreAnterior ?? 0)) / stats.total;
  }

  return proximo;
}

/**
 * Incrementa o balde da hora a que a transação pertence. Se ela cair fora da
 * janela de 24h desenhada (relógio do cliente adiantado, evento atrasado na
 * fila), o gráfico fica como está — inventar um balde novo distorceria a
 * escala do eixo por causa de um caso de borda.
 */
export function somaNaHora(timeline: VolumeHora[], tx: TransacaoDto): VolumeHora[] {
  if (timeline.length === 0) return timeline;

  const quando = new Date(tx.timestamp).getTime();
  let alvo = -1;
  for (let i = timeline.length - 1; i >= 0; i--) {
    if (quando >= new Date(timeline[i].hora).getTime()) {
      alvo = i;
      break;
    }
  }
  if (alvo < 0) return timeline;

  const chave = chaveStatus(tx.status);
  return timeline.map((v, i) =>
    i !== alvo
      ? v
      : {
          ...v,
          quantidade: v.quantidade + 1,
          valor: v.valor + tx.valor,
          bloqueadas: v.bloqueadas + (chave === 'bloqueada' ? 1 : 0),
          suspeitas: v.suspeitas + (chave === 'suspeita' ? 1 : 0),
        },
  );
}

/** Insere no topo de uma lista sem duplicar, respeitando um teto. */
export function insereNoTopo(
  lista: TransacaoDto[],
  tx: TransacaoDto,
  teto: number,
): TransacaoDto[] {
  return [tx, ...lista.filter(t => t.id !== tx.id)].slice(0, teto);
}

/** Reescreve uma linha em qualquer lista onde ela apareça. */
export function aplicaPatch(
  lista: TransacaoDto[],
  id: string,
  campos: Partial<TransacaoDto>,
): TransacaoDto[] {
  let mudou = false;
  const proxima = lista.map(t => {
    if (t.id !== id) return t;
    mudou = true;
    return { ...t, ...campos };
  });
  return mudou ? proxima : lista;
}

/** Uma transação combina com o filtro ativo do livro? */
export function casaFiltro(tx: TransacaoDto, filtro: string): boolean {
  return filtro === '' || chaveStatus(tx.status) === chaveStatus(filtro);
}
