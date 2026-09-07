# VERO — Painel

Front-end do boletim de integridade transacional. React + TypeScript + Vite,
com SignalR para o fluxo ao vivo.

```bash
npm install
npm run dev            # http://localhost:5173
npm run dev -- --open  # com a API em http://localhost:5000
```

Sem a API no ar, abra `http://localhost:5173/?demo` — o parâmetro `?demo` troca
as chamadas por dados sintéticos (`src/demo.ts`) e emite eventos falsos no feed.
Serve para revisar a interface sem subir API, Postgres e worker. Nenhum caminho
normal passa por esse código.

## Direção visual

A tela é **uma folha de papel com uma faixa de telemetria cravada no meio**.
Editorial suíço na composição, painel de instrumentos no comportamento.

| Decisão | Onde |
|---|---|
| Terra clara (papel `#E8E4D9`), tinta `#14120F` | `src/index.css` |
| Faixa invertida em sangria total, atravessando a folha | `.band` |
| Grade assimétrica 5/4/2 — número monumental, pilha de leituras, proporção | `.situation` |
| Marcas de registro nos cantos, como peça impressa | `.sheet::before/::after` |
| Trilho vertical com a marca, o estado do enlace e a inversão | `.rail` |

**Tipos.** `Archivo` (eixo de largura variável) nos números e títulos,
`IBM Plex Mono` em todo dado técnico, `Newsreader` itálico nas notas
editoriais. Nada de Inter.

**Cor.** Quatro sinais e nada mais: vermelhão `--signal` (bloqueio), âmbar
`--amber` (suspeita), verde `--verde` (aprovada), azul `--azul` (pendente).
Cor aqui carrega estado; nunca é enfeite.

**Modo tinta.** O quadrado meio-preto-meio-branco no pé do trilho inverte a
folha: o papel vira preto e a faixa de telemetria vira papel. É uma troca de
tokens (`[data-mode="ink"]`), guardada em `localStorage` e aplicada antes da
primeira pintura no `index.html`. A troca é instantânea, de propósito — um
crossfade passaria por um cinza morto.

**Marca.** Um punção de conferência: campo de inspeção quadrado, marcas de
registro nas laterais, fio de leitura no meio e um V que rompe a base do quadro.
Desenhada em `src/components/Mark.tsx`, traçada na montagem.

## Movimento

Cada tipo de dado entra de um jeito diferente — nada de fade-in em tudo.

- Fios e sublinhados são **traçados** (`scaleX` a partir da esquerda).
- O número monumental é **revelado por corte** (`clip-path`), com contagem
  crescente por trás.
- As colunas do gráfico **crescem da linha de base**, 16ms de defasagem entre
  elas; a linha de taxa é desenhada com `stroke-dashoffset`.
- As linhas do feed entram por **varredura lateral** e dão um único lampejo.
- Tudo entre 200ms e 500ms, com `prefers-reduced-motion` respeitado.

## Gráfico

`src/components/VolumeChart.tsx` é SVG escrito à mão — sem biblioteca de
gráficos. O cursor move um fio vertical, escurece as demais colunas e alimenta
a leitura no canto superior direito (hora, fluxo, bloqueios, suspeitas, taxa).
