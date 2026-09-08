import { useEffect, useMemo, useRef, useState } from 'react';

/**
 * CONSOLE — a régua de comando
 *
 * Padrão emprestado do "command palette" (⌘K): uma linha só, teclado no
 * comando, tudo alcançável sem caçar botão. O que não veio junto foi a
 * caixa flutuante arredondada com sombra — aqui o console é um *carimbo
 * de expediente* que desce da borda de cima da folha: fio duro, canto
 * reto, fundo de papel, régua de tinta no topo.
 *
 * A entrada é um corte (clip-path), não um desbotamento, e as linhas
 * varrem da esquerda em cascata curta. A linha sob o cursor não ganha
 * halo: ela inverte papel e tinta, de uma vez, como um relé.
 *
 * O componente é montado só quando aberto — é isso que dispensa qualquer
 * efeito de "limpar o estado ao abrir": abrir *é* montar.
 */

export interface Comando {
  id: string;
  /** Agrupador impresso acima do bloco — "filtrar", "ir para", "folha". */
  grupo: string;
  rotulo: string;
  /** Texto à direita: atalho, estado atual ou consequência. */
  dica?: string;
  /** Sinônimos que também devem casar na busca. */
  termos?: string;
  /** Marca o comando como já em vigor (filtro ativo, modo corrente). */
  ativo?: boolean;
  executar: () => void;
}

/** Sem acento e sem caixa — quem digita "analise" tem de achar "Em análise". */
function planifica(s: string) {
  return s
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase();
}

/**
 * Casamento por subsequência: "exp" acha "Exportar página". É o contrato de
 * qualquer paleta de comando decente — o operador digita as consoantes de
 * que lembra, não o rótulo inteiro.
 */
function casa(alvo: string, busca: string) {
  if (!busca) return true;
  let i = 0;
  for (const c of alvo) {
    if (c === busca[i]) i++;
    if (i === busca.length) return true;
  }
  return false;
}

interface Props {
  comandos: Comando[];
  onFechar: () => void;
}

export function Console({ comandos, onFechar }: Props) {
  const [busca, setBusca] = useState('');
  const [cursor, setCursor] = useState(0);
  const campoRef = useRef<HTMLInputElement>(null);
  const listaRef = useRef<HTMLDivElement>(null);

  const filtrados = useMemo(() => {
    const b = planifica(busca.trim());
    return comandos.filter(c => casa(planifica(`${c.grupo} ${c.rotulo} ${c.termos ?? ''}`), b));
  }, [comandos, busca]);

  // O foco tem de esperar o elemento existir de fato no documento.
  useEffect(() => {
    const t = requestAnimationFrame(() => campoRef.current?.focus());
    return () => cancelAnimationFrame(t);
  }, []);

  // O cursor pode sair da janela rolável quando se navega só pelo teclado.
  useEffect(() => {
    listaRef.current?.querySelector('.console-item.on')?.scrollIntoView({ block: 'nearest' });
  }, [cursor]);

  const disparar = (c: Comando | undefined) => {
    if (!c) return;
    onFechar();
    c.executar();
  };

  const aoTeclar = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') {
      e.preventDefault();
      onFechar();
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      setCursor(i => (filtrados.length ? (i + 1) % filtrados.length : 0));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setCursor(i => (filtrados.length ? (i - 1 + filtrados.length) % filtrados.length : 0));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      disparar(filtrados[cursor]);
    }
  };

  // O grupo só é impresso quando muda — repetir o rótulo em toda linha seria
  // ruído numa lista que já é curta.
  let grupoAnterior = '';

  return (
    <div className="console-veu" onMouseDown={onFechar}>
      <div
        className="console"
        role="dialog"
        aria-modal="true"
        aria-label="Console de comando"
        onMouseDown={e => e.stopPropagation()}
      >
        <div className="console-linha">
          <span className="console-marca">⌘K</span>
          <input
            ref={campoRef}
            className="console-campo"
            value={busca}
            onChange={e => {
              setBusca(e.target.value);
              // Digitar refaz a lista inteira; manter o cursor onde estava
              // faria o Enter disparar um comando que não é mais o primeiro.
              setCursor(0);
            }}
            onKeyDown={aoTeclar}
            placeholder="filtrar, navegar, exportar…"
            spellCheck={false}
            autoComplete="off"
          />
          <span className="console-conta">{String(filtrados.length).padStart(2, '0')}</span>
          <button className="console-esc" onClick={onFechar}>
            esc
          </button>
        </div>

        <div className="console-lista" ref={listaRef}>
          {filtrados.length === 0 ? (
            <div className="console-vazio">Nenhum comando sob esta busca</div>
          ) : (
            filtrados.map((c, i) => {
              const abreGrupo = c.grupo !== grupoAnterior;
              grupoAnterior = c.grupo;

              return (
                <div key={c.id}>
                  {abreGrupo && <div className="console-grupo">{c.grupo}</div>}
                  <button
                    className={`console-item${i === cursor ? ' on' : ''}`}
                    style={{ animationDelay: `${Math.min(i, 14) * 18}ms` }}
                    onMouseMove={() => setCursor(i)}
                    onClick={() => disparar(c)}
                  >
                    <span className="console-num">{String(i + 1).padStart(2, '0')}</span>
                    <span className="console-rot">{c.rotulo}</span>
                    {c.ativo && <span className="console-vig">em vigor</span>}
                    {c.dica && <span className="console-dica">{c.dica}</span>}
                  </button>
                </div>
              );
            })
          )}
        </div>

        <div className="console-rodape">
          <span>↑↓ percorrer</span>
          <span>⏎ executar</span>
          <span>esc fechar</span>
        </div>
      </div>
    </div>
  );
}
