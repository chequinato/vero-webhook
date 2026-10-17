# 12 — Estado ao vivo no cliente

> **🧠 Pra gravar**
> - **Otimismo + reconciliação**: aplica o evento localmente na hora; o servidor manda a verdade depois.
> - **Debounce** = "faça quando parar de acontecer" · **throttle** = "no máximo uma vez a cada X".
> - **Imutabilidade**: devolva objeto novo — o React compara por identidade.
> - **Idempotência**: aplicar N vezes = aplicar uma vez. Em sistema distribuído, mensagem duplicada sempre pode chegar.

Estes padrões valem para **qualquer** aplicação que receba eventos, não só para
este painel.

## O problema

- **Buscar a cada dez segundos** → o painel fica até dez segundos atrasado.
- **Buscar a cada evento** → uma rajada de 300 transações vira 300 requisições, a API sofre e a tela pisca.

## A solução: atualização otimista + reconciliação

1. **Otimismo** — quando o evento chega, o cliente **recalcula os agregados
   localmente**, no mesmo quadro. Custo: zero requisições.
2. **Reconciliação** — algum tempo depois, o servidor manda a verdade e
   sobrescreve. Se o cálculo local errou, o erro dura segundos.

```typescript
// App.tsx — chegou uma transação pelo hub
const aoChegar = useCallback((tx: TransacaoDto) => {
  lancar(tx);                                        // cena 3D (barramento próprio)
  setStats(s => (s ? somaTransacao(s, tx) : s));     // agregados
  setTimeline(t => somaNaHora(t, tx));               // coluna da hora
  setAmostra(a => insereNoTopo(a, tx, AMOSTRA));     // anel, histograma, matriz
  setTotal(n => n + 1);

  // Só a primeira página recebe a linha nova; nas outras, a linha que o
  // operador está lendo escorregaria enquanto ele lê.
  if (page === 1 && casaFiltro(tx, filter)) {
    setTransactions(list => insereNoTopo(list, tx, PAGINA));
  }

  agendarReconciliacao();
}, [page, filter, agendarReconciliacao]);
```

**Atualização ao vivo não pode atropelar a leitura de quem está trabalhando.**

## Reconciliação preguiçosa (debounce)

```typescript
const QUIETUDE = 2500;

const agendarReconciliacao = useCallback((atraso = QUIETUDE) => {
  clearTimeout(reconRef.current);                    // cancela a anterior
  reconRef.current = window.setTimeout(() => setRefreshKey(k => k + 1), atraso);
}, []);
```

Cada evento cancela a busca agendada e agenda outra. Numa rajada contínua a
busca não dispara — os agregados locais já estão certos. Um `setInterval` de
dez segundos serve de **piso**, para o servidor ter a palavra final de tempos
em tempos.

## Imutabilidade

As funções de `live.ts` **nunca modificam o que recebem**:

```typescript
export function somaTransacao(stats: Stats, tx: TransacaoDto): Stats {
  const total = stats.total + 1;
  const valorTotal = stats.valorTotal + tx.valor;

  const proximo: Stats = {
    ...stats,                    // copia o anterior
    total,
    valorTotal,
    valorMedio: valorTotal / total,
    // média corrida: repondera sem guardar a soma de todos os escores
    riskScoreMedio: (stats.riskScoreMedio * stats.total + (tx.riskScore ?? 0)) / total,
  };
  // ...
  return proximo;
}
```

1. **O React compara por identidade** (`Object.is`). Mutar `stats.total++` e
   devolver o mesmo objeto não repinta nada.
2. **O contador precisa do valor anterior** para saber quais dígitos girar.
   Mutação destrói o passado.

## Idempotência

Ao reavaliar, o mesmo desfecho chega **duas vezes**: na resposta do `POST` e no
eco do hub. Aplicar duas vezes precisa ser inofensivo:

```typescript
const antes = conhecidas.current.get(p.id);
if (antes && antes.status === p.status && antes.riskScore === p.riskScore) return;
```

`x = 5` é idempotente; `x++` não é. No backend, a checagem de duplicata no
`TransacaoService` e a guarda de status no `AnomalyDetectionService` cumprem o
mesmo papel.

## `useSyncExternalStore`

A telemetria vive fora do React. Para o React acompanhá-la:

```typescript
let versao = 0;
const ouvintes = new Set<() => void>();

function emitir() { versao++; for (const fn of ouvintes) fn(); }
function inscrever(fn: () => void) { ouvintes.add(fn); return () => { ouvintes.delete(fn); }; }

export function useTelemetria(): Snapshot {
  const v = useSyncExternalStore(inscrever, () => versao, () => 0);
  return useMemo(() => ler(), [v]);
}
```

**A armadilha:** `getSnapshot` tem que devolver o **mesmo valor** enquanto nada
mudou. Um objeto novo a cada chamada põe o React em laço infinito de
repintura. Por isso ele devolve `versao` (um número) e o snapshot é memoizado
por versão. O terceiro argumento é o snapshot para renderização no servidor.

## Barramento de eventos fora do React

A cena 3D roda a 60 quadros por segundo. Passar cada transação por `setState`
repintaria o painel inteiro por causa de um ponto que se move. A saída:

```typescript
// engine/bus.ts
type Ouvinte = (tx: TransacaoDto) => void;
const ouvintes = new Set<Ouvinte>();

export function lancar(tx: TransacaoDto) { for (const fn of ouvintes) fn(tx); }

export function assinar(fn: Ouvinte): () => void {
  ouvintes.add(fn);
  return () => { ouvintes.delete(fn); };
}
```

A cena assina uma vez e empurra direto para a GPU; o React nunca fica sabendo.
É o **padrão Observer** — o mesmo do `INotificadorTransacao` no backend.

## Mock que não se mexe engana

A primeira versão do modo `?demo` servia constantes calculadas na carga. Os
eventos apareciam no feed, mas nenhum indicador mexia — parecia bug da
interface, e era o dado. A correção deu **estado mutável** ao módulo: cada
evento entra no livro e os agregados são recalculados a cada leitura.

**Se a fonte real muda, o dublê de teste também tem que mudar.**

---

**Anterior:** [← 11 — Dashboard React](11-dashboard-react.md) · **Próximo:** [13 — WebGL →](13-webgl.md)
