import { useEffect } from 'react';

/**
 * Abre a régua de comando em ⌘K / Ctrl+K.
 *
 * Mora fora do componente porque o atalho tem de valer com o console
 * fechado — ou seja, justamente quando ele não está montado na árvore.
 */
export function useAtalhoConsole(abrir: () => void) {
  useEffect(() => {
    const aoTeclar = (e: KeyboardEvent) => {
      if (e.key.toLowerCase() === 'k' && (e.metaKey || e.ctrlKey)) {
        e.preventDefault();
        abrir();
      }
    };
    window.addEventListener('keydown', aoTeclar);
    return () => window.removeEventListener('keydown', aoTeclar);
  }, [abrir]);
}
