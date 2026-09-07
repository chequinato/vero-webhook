import { useEffect, useState } from 'react';

/**
 * Contador mecânico.
 *
 * Cada dígito é uma coluna 0–9 que desliza atrás de uma janela. Quando o
 * número muda, só as colunas que realmente mudaram se movem — é assim que
 * um contador de painel se comporta, e é o que separa isto de um número
 * que simplesmente aparece. A defasagem entre colunas é curta (28ms) para
 * o conjunto assentar como um mecanismo só, não como dez animações.
 *
 * Requer fonte com algarismos tabulares, senão a janela balança.
 */

interface Props {
  value: number;
  /** Casas decimais; o separador entra como glifo fixo, sem rolar. */
  decimals?: number;
  prefix?: string;
  suffix?: string;
  className?: string;
  /** Duração da rolagem de cada coluna. */
  duration?: number;
}

export function Odometer({
  value,
  decimals = 0,
  prefix = '',
  suffix = '',
  className = '',
  duration = 620,
}: Props) {
  // Primeira pintura em zero: a rolagem inicial é a entrada do elemento.
  const [mostrado, setMostrado] = useState(0);

  useEffect(() => {
    // Dois gatilhos de propósito: o quadro de animação dá a rolagem no
    // momento certo quando a aba está visível, e o temporizador garante
    // que o valor chegue mesmo quando o navegador estrangula o rAF (aba
    // em segundo plano, janela oculta). Sem o segundo, o contador ficaria
    // parado em zero — um número errado é pior que um número sem graça.
    const quadro = requestAnimationFrame(() => setMostrado(value));
    const rede = setTimeout(() => setMostrado(value), 48);
    return () => {
      cancelAnimationFrame(quadro);
      clearTimeout(rede);
    };
  }, [value]);

  const fmt = (v: number) =>
    v.toLocaleString('pt-BR', {
      minimumFractionDigits: decimals,
      maximumFractionDigits: decimals,
    });

  const atual = fmt(mostrado);
  const destino = fmt(value);

  // O gabarito é sempre a forma mais longa das duas, para a largura da
  // janela não pular no meio da rolagem; a mais curta ganha zeros à
  // esquerda, exatamente como um contador de painel faz.
  const gabarito = atual.length >= destino.length ? atual : destino;
  const texto = atual.padStart(gabarito.length, '0');

  let coluna = 0;

  return (
    <span className={`od ${className}`.trim()}>
      {prefix && <span className="od-fixed">{prefix}</span>}
      {gabarito.split('').map((ch, i) => {
        if (!/\d/.test(ch)) {
          return (
            <span className="od-fixed" key={`s${i}`}>
              {ch}
            </span>
          );
        }
        const bruto = texto[i] ?? '0';
        const d = /\d/.test(bruto) ? Number(bruto) : 0;
        const atraso = coluna++ * 28;
        return (
          <span className="od-col" key={`d${i}`}>
            <span
              className="od-strip"
              style={{
                transform: `translateY(${-d * 10}%)`,
                transitionDuration: `${duration}ms`,
                transitionDelay: `${atraso}ms`,
              }}
            >
              {['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'].map(n => (
                <span key={n}>{n}</span>
              ))}
            </span>
          </span>
        );
      })}
      {suffix && <span className="od-fixed">{suffix}</span>}
    </span>
  );
}
