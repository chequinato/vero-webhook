import type { TransacaoDto } from '../types';

/**
 * BARRAMENTO DO MOTOR
 *
 * A cena 3D roda a 60 quadros por segundo e não pode depender do ciclo de
 * render do React: uma transação por segundo virando `setState` obrigaria a
 * árvore inteira a repintar por causa de um ponto que se move. Então os
 * eventos chegam por aqui, direto na cena, sem passar pelo React.
 *
 * O painel continua recebendo os mesmos eventos pelo caminho normal — este
 * barramento é um desvio paralelo, não um substituto.
 */

type Ouvinte = (tx: TransacaoDto) => void;

const ouvintes = new Set<Ouvinte>();

export function lancar(tx: TransacaoDto) {
  for (const fn of ouvintes) fn(tx);
}

export function assinar(fn: Ouvinte): () => void {
  ouvintes.add(fn);
  return () => {
    ouvintes.delete(fn);
  };
}
