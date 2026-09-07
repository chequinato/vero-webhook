# VERO — Painel

Front-end do boletim de integridade transacional. React + TypeScript + Vite,
com SignalR para o fluxo ao vivo.

```bash
npm install
npm run dev            # http://localhost:5173
npm run dev -- --open  # com a API em http://localhost:5000
```

Sem a API no ar, abra `http://localhost:5173/?demo` — o parâmetro `?demo` troca
as chamadas por dados sintéticos (`src/demo.ts`) e emite eventos no feed. Esse
módulo guarda **estado mutável**: cada evento cria uma transação de verdade,
empilha no livro e passa a contar nos agregados, então o painel se mexe em
demonstração pelo mesmo caminho por que se mexeria com a API. Serve para
revisar a interface sem subir API, Postgres e worker. Nenhum caminho normal
passa por esse código.

Para rodar contra a API de verdade sem Postgres:

```bash
# na raiz, em outro terminal
dotnet run --project src/Vero.Api -- --dev   # SQLite em memória, dados semeados
```

## Direção visual

A tela é **uma folha de papel com faixas de telemetria cravadas nela**.
Editorial suíço na composição, painel de instrumentos no comportamento.

O ritmo da página alterna suporte: faixa · papel · faixa · papel · faixa · papel.
A primeira coisa que se vê não é um número parado — é a máquina rodando.

| Seção | Suporte | Onde |
|---|---|---|
| 00 Motor de decisão (3D, ao vivo) | faixa invertida | `Engine.tsx`, `engine/` |
| 01 Situação | papel | `src/components/Situation.tsx` |
| 02 Fluxo por hora · 03 Feed ao vivo | faixa invertida | `VolumeChart.tsx`, `Feed.tsx` |
| 04 Composição (anel, distribuição, curva de operação, matriz) | papel | `Composition.tsx` |
| 05 Observabilidade do enlace | faixa invertida | `Observability.tsx` |
| 06 Registro + fila de avaliação | papel | `Ledger.tsx` |

Outras decisões estruturais:

| Decisão | Onde |
|---|---|
| Terra clara (papel `#E8E4D9`), tinta `#14120F` | `src/index.css` |
| Faixa invertida em sangria total, atravessando a folha | `.band` |
| Grade assimétrica 5/4/2 — número monumental, pilha de leituras, proporção | `.situation` |
| Marcas de registro nos cantos, como peça impressa | `.sheet::before/::after` |
| Trilho vertical com a marca, a disponibilidade e a inversão | `.rail` |

**Tipos.** A pilha de display resolve para a **São Francisco do sistema** em Mac
e iPhone (`-apple-system`, `BlinkMacSystemFont`, `SF Pro Display`) e cai em
**Geist** fora do ecossistema Apple — mesma métrica, mesmo desenho neogrotesco.
O dado técnico é `SF Mono`/`Geist Mono`; as notas editoriais são `Newsreader`
itálico. Nenhum eixo de largura variável: o **peso** carrega a hierarquia, com
os indicadores principais em 800 e tracking negativo, que é o gesto da SF Pro.
Nada de Inter.

**Cor.** Quatro sinais e nada mais: vermelhão `--signal` (bloqueio), âmbar
`--amber` (suspeita), verde `--verde` (aprovada), azul `--azul` (pendente).
Cor aqui carrega estado; nunca é enfeite.

**Modo tinta.** O quadrado meio-preto-meio-branco no pé do trilho inverte a
folha: o papel vira preto e as faixas de telemetria viram papel. É uma troca de
tokens (`[data-mode="ink"]`), guardada em `localStorage` e aplicada antes da
primeira pintura no `index.html`. A troca é instantânea, de propósito — um
crossfade passaria por um cinza morto.

**Marca.** Um punção de conferência: campo de inspeção quadrado, marcas de
registro nas laterais, fio de leitura no meio e um V que rompe a base do quadro.
Desenhada em `src/components/Mark.tsx`, traçada na montagem.

## Movimento

Cada tipo de dado entra de um jeito diferente — nada de fade-in em tudo.

- Fios e sublinhados são **traçados** (`scaleX` a partir da esquerda).
- Números são **contadores mecânicos** (`Odometer.tsx`): cada dígito é uma
  coluna 0–9 atrás de uma janela, e só as colunas que mudaram se movem, com
  28ms de defasagem. O contador tem rede de segurança em `setTimeout` para o
  caso de o navegador estrangular o `requestAnimationFrame` — um número preso
  em zero é pior que um número sem graça.
- Arcos (anel de composição, manômetro) são **desenhados** por
  `stroke-dashoffset`, um setor de cada vez.
- As colunas do gráfico **crescem da linha de base**; as barras da distribuição
  entram em frente de onda da esquerda para a direita.
- As células da matriz abrem em **frente de onda diagonal** (`linha + coluna`).
- As linhas do feed entram por **varredura lateral** e dão um único lampejo.
- A cada sincronia, **um fio atravessa a faixa de telemetria uma vez**
  (`.band-sweep`) — recibo de que os dados chegaram, não enfeite.
- Tudo entre 200ms e 700ms, com `prefers-reduced-motion` respeitado.

## Gráficos

Todos são SVG escrito à mão — nenhuma biblioteca de gráficos.

- **`VolumeChart.tsx`** — colunas empilhadas por hora com a taxa de bloqueio
  traçada por cima. O cursor move um fio vertical, escurece as demais colunas e
  alimenta a leitura no canto.
- **`Donut.tsx`** — anel de composição desenhado como mostrador de instrumento:
  escala gravada a cada 2,5%, setores traçados em sequência e o setor sob o
  cursor sai do anel na direção da própria bissetriz.
- **`RiskHistogram.tsx`** — vinte faixas de escore com os **cortes de decisão do
  motor** (40% revisão, 70% bloqueio) impressos por cima, mais a acumulada.
  O ponto do gráfico é ver quanta massa está encostada em cada corte.
- **`Threshold.tsx`** — curva de operação: para cada corte possível, quantas
  linhas e quanto **valor** ficariam retidos. Divide o eixo horizontal com o
  histograma acima de propósito, e o corte simulado é arrastável (ponteiro ou
  setas do teclado) — mover ali move a vertical projetada na distribuição. As
  duas curvas existem porque contagem e dinheiro não andam juntos: o vão entre
  elas é o argumento inteiro para mexer no limiar.
- **`Matrix.tsx`** — tabela de contingência tipo × faixa de risco. A intensidade
  é tinta sólida, não arco-íris; o cursor acende a linha e a coluna inteiras.
- **`Gauge.tsx`** / **`Sparkline.tsx`** — manômetro de 240° e traço de série
  curta, usados na faixa de observabilidade.

## Observabilidade

`src/telemetry.ts` é um registro em memória alimentado por três fontes reais:

1. `sonda()` cronometra **toda** chamada de `src/api.ts` com `performance.now()`
   e guarda duração e desfecho;
2. `registrarEvento()` marca cada evento entregue pelo hub SignalR;
3. um laço de `requestAnimationFrame` amostra quadros por segundo, descartando
   janelas em que a aba não estava visível.

Disso saem percentis de latência, fita de disponibilidade em fatias de 5s,
quebra por rota, vazão e consumo do orçamento de erro (alvo 99,5%).

**Nada nessa seção é estimado.** Ela descreve o enlace navegador→API desta
sessão, não o serviço em produção — e quando não há medida, mostra `——` em vez
de inventar um número. Um painel de observabilidade que fabrica dados é pior
que não ter painel.


## Atualização ao vivo

O painel não espera a próxima busca para se mexer. `src/live.ts` recalcula os
agregados no mesmo quadro em que o evento chega pelo hub — o contador rola, a
coluna da hora cresce, o anel se redistribui, a linha aparece no topo do livro.
A busca no servidor vem depois, só para reconciliar.

A reconciliação é **preguiçosa**: agendada para 2,5s depois do último evento e
cancelada a cada novo, com um piso de 10s garantido por intervalo. Numa rajada,
buscar a cada evento derrubaria a API e faria a tela piscar sem necessidade —
os agregados locais já estão certos no intervalo.

Detalhes que o código carrega e que valem a leitura:

- páginas internas do livro **não** recebem linhas novas; só a primeira. Senão
  a linha que o operador está lendo escorregaria enquanto ele lê;
- o mesmo desfecho chega duas vezes numa reavaliação (resposta do `POST` e eco
  do hub). Aplicar é idempotente, e o segundo é descartado por comparação;
- mudança de status ajusta a distribuição localmente, mas não os baldes da
  hora — para isso faltaria o carimbo original, então o servidor resolve.

## Fila de avaliação

Toda linha em revisão (`suspeita` ou `aceita_provisoria`) ganha um punção
`avaliar` na última coluna do registro. Apertar não decide nada: chama
`POST /api/dashboard/transactions/{id}/reavaliar`, e **quem decide é o modelo**
— acima do corte de 70% vira bloqueada, abaixo vira aprovada.

Enquanto a chamada está no ar, um fio varre a base do botão; quando o veredito
chega, a linha se resolve na frente do operador e fica marcada na margem com a
cor do desfecho, como um visto a lápis. Linhas já decididas não têm botão, e a
API recusa reavaliá-las com 409.


## O motor (seção 00)

A faixa do topo é uma cena WebGL: a malha é o **pipeline real do Vero** e cada
ponto que a atravessa é uma transação de verdade.

```
barramento → regras síncronas → modelo FastTree → regras assíncronas → decisão
    1 nó           2 nós              7 nós              3 nós          4 portões
```

Os nomes saem do backend: as síncronas são `ValorAltoRule` e `ScoreBaixoRule`,
as sete do meio são exatamente as features que `MlRiskScoringService.
ExtrairFeatures` monta, e as assíncronas são `VelocityRule`,
`HorarioEstranhoRule` e `ValorRedondoRule` (`src/engine/topologia.ts`).

**Como se liga ao resto.** Na abertura, a amostra de 100 linhas já buscada
atravessa a máquina uma vez — é a carga inicial. Depois, cada evento do hub
entra pelo `src/engine/bus.ts`, um barramento próprio que **não passa pelo
ciclo de render do React**: uma transação por segundo virando `setState`
obrigaria a árvore inteira a repintar por causa de um ponto que se move.

**Interação.** O ponteiro orbita a malha (paralaxe); passar sobre um nó acende
suas arestas e mostra quantas transações já passaram por ele nesta sessão;
clicar num portão de decisão filtra o registro por aquele estado e desce até
ele.

**Honestidade.** O desfecho e o escore de cada partícula são reais — vêm da
API, e o portão em que ela pousa é a decisão que o backend tomou. O **trajeto
pelo miolo é ilustrativo**: a API não devolve atribuição por feature, então o
roteamento é derivado dos campos que temos (valor, hora, motivo, escore) de
forma determinística — a mesma transação desenha sempre o mesmo caminho. A
cena imprime essa ressalva no próprio cabeçalho.

**Disciplina visual.** Blending normal, quadrados de aresta dura, zero bloom e
zero gradiente. A cor só entra no último trecho, quando a decisão acontece: até
ali a partícula é tinta neutra. A paleta é lida do CSS em tempo de execução,
então o modo tinta continua sendo uma troca de tokens — inclusive dentro do
WebGL, onde as arestas são interpoladas **entre o fundo da faixa e a tinta**,
nunca escurecidas (escurecer viraria risco preto sobre papel claro).

**Custo.** `three` entra por `React.lazy` num pedaço separado (~530 kB, 134 kB
comprimido). A folha pinta antes; a faixa reserva a altura e mostra "montando o
motor" enquanto o pedaço chega. O laço pausa quando a faixa sai da tela
(`IntersectionObserver`) ou a aba fica oculta, e há caminho de degradação
quando o navegador não tem WebGL.
