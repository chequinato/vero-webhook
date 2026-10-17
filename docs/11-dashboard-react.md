# 11 — Dashboard React

> **🧠 Pra gravar**
> - Sem biblioteca de gráficos nem de ícones: **SVG e CSS escritos à mão**, para controlar cada pixel e cada animação.
> - `useEffect` que **assina** algo tem que **desassinar** no retorno.
> - `useCallback` não é "otimização": existe porque a **identidade** da função importa nas dependências.
> - Toda chamada HTTP passa por uma **sonda** que mede latência — o painel mede em vez de estimar.

## Stack

| Tecnologia | Para quê |
|---|---|
| React 19 | Componentes reativos |
| TypeScript 6 | Tipagem estática sobre JavaScript |
| Vite 8 | Dev server com hot reload e bundler |
| `@microsoft/signalr` | Cliente WebSocket do hub da API |
| `three` | WebGL — a cena 3D do motor de decisão |
| oxlint | Linter escrito em Rust, muito mais rápido que ESLint |

## Estrutura

```
dashboard/src/
├── main.tsx            # monta o React no DOM
├── App.tsx             # busca dados, aplica eventos, compõe as seções
├── api.ts              # cliente HTTP tipado; toda chamada passa pela sonda
├── types.ts            # interfaces que espelham os DTOs do C#
├── live.ts             # funções puras que aplicam eventos aos agregados
├── telemetry.ts        # latência e disponibilidade
├── demo.ts             # dados sintéticos vivos (?demo na URL)
├── useSignalR.ts       # conexão com o hub
├── engine/
│   ├── bus.ts          # barramento de eventos fora do React
│   └── topologia.ts    # nós, arestas e roteamento da cena 3D
├── hooks/              # useClock, useWidth
└── components/         # Engine, Situation, VolumeChart, Feed, Composition,
                        # Observability, Ledger, Odometer...
```

Sem a API no ar, `http://localhost:5173/?demo` usa dados sintéticos que se
mexem de verdade.

## Hooks — o que cada um resolve

| Hook | Problema que resolve |
|---|---|
| `useState` | Estado local que, ao mudar, repinta o componente |
| `useEffect` | Sincronizar com algo **fora** do React (rede, timer, WebGL, DOM) |
| `useCallback` | Manter a **identidade** de uma função entre renders |
| `useMemo` | Não recalcular derivações caras a cada render |
| `useRef` | Guardar valor mutável **sem** repintar |
| `useSyncExternalStore` | Ler de uma fonte de dados externa ao React |
| `lazy` + `Suspense` | Carregar código sob demanda |

### Por que a identidade da função importa

```tsx
// ✗ Se `onFiltrar` estiver nas dependências do efeito que monta o WebGL,
//   cada repintura do pai destrói e reconstrói a cena inteira.
useEffect(() => { /* monta cena */ }, [onFiltrar]);

// ✓ A função vive numa referência; o efeito não depende dela.
const filtrarRef = useRef(onFiltrar);
useEffect(() => { filtrarRef.current = onFiltrar; }, [onFiltrar]);
useEffect(() => { /* monta cena, usa filtrarRef.current */ }, []);
```

## Hook customizado

Função que começa com `use` e chama outros hooks. Dá **nome** a uma lógica:

```typescript
// hooks/useWidth.ts — largura corrente de um elemento
export function useWidth(inicial = 640) {
  const ref = useRef<HTMLDivElement>(null);
  const [w, setW] = useState(inicial);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const ro = new ResizeObserver(([entry]) => setW(entry.contentRect.width));
    ro.observe(el);
    setW(el.clientWidth || inicial);
    return () => ro.disconnect();       // limpeza: sem isso, vaza observer
  }, [inicial]);

  return [ref, Math.max(120, w)] as const;   // `as const` → tupla, não array
}
```

`ResizeObserver`, `setInterval`, `addEventListener`, conexão SignalR, contexto
WebGL: **tudo que é assinado precisa ser desassinado**, senão cada montagem
deixa lixo rodando.

## Cliente HTTP tipado

```typescript
async function fetchJson<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${API_BASE}${path}`, init);
  if (!res.ok) {
    // A API devolve { erro } nos casos previstos (404, 409) — mostra a mensagem dela
    const corpo = await res.json().catch(() => null);
    throw new Error(corpo?.erro ?? `API error: ${res.status}`);
  }
  return res.json();
}

reavaliar: (id: string) =>
  chamar('reavaliar', () =>
    fetchJson<Reavaliacao>(`/api/dashboard/transactions/${encodeURIComponent(id)}/reavaliar`,
      { method: 'POST' })),
```

1. **`<T>`** — o mesmo `fetchJson` serve para qualquer rota, com o tipo certo de retorno. É o `Task<T>` do C#.
2. **`encodeURIComponent`** — o id vem de dados; interpolar direto na URL é como concatenar SQL.
3. **`chamar(...)`** — passa pela sonda de telemetria.

## Tipos que espelham os DTOs do C#

```typescript
export interface TransacaoDto {
  id: string;
  valor: number;              // decimal → number
  status: string;
  riskScore: number | null;   // float? → union com null
  motivo: string | null;
  timestamp: string;          // DateTime → string ISO
}
```

São escritos à mão e **podem sair de sincronia com o C#**. Em projetos maiores
isso se resolve com OpenAPI + gerador de cliente.

`import type { ... }` importa só o tipo: some do bundle, porque tipo não existe
em tempo de execução.

## Medir em vez de estimar

```typescript
export async function sonda<T>(rota: string, exec: () => Promise<T>): Promise<T> {
  const t0 = performance.now();
  try {
    const r = await exec();
    registrar(rota, performance.now() - t0, true);
    return r;
  } catch (err) {
    registrar(rota, performance.now() - t0, false);   // falha também é medida
    throw err;                                        // e o erro segue adiante
  }
}
```

A sonda **observa sem alterar o comportamento**: engolir o erro transformaria
falha de rede em sucesso silencioso. Daí saem percentis (p50/p90/p99),
disponibilidade e quebra por rota. Sem amostra, a tela mostra `——`: **um
número inventado é pior que um espaço vazio**.

## Animar a partir dos dados

O `Odometer` (contador mecânico) mostra o padrão "o React decide o estado
final, o CSS faz a transição":

```tsx
const [mostrado, setMostrado] = useState(0);   // primeira pintura em zero

useEffect(() => {
  // rAF dá a rolagem no momento certo; o timeout garante o valor mesmo se
  // o navegador estrangular o rAF (aba em segundo plano)
  const quadro = requestAnimationFrame(() => setMostrado(value));
  const rede = setTimeout(() => setMostrado(value), 48);
  return () => { cancelAnimationFrame(quadro); clearTimeout(rede); };
}, [value]);
```

Cada dígito é uma coluna 0–9 atrás de uma janela `overflow: hidden`; mudar o
valor muda o `translateY`. Sem biblioteca de animação, e o efeito para sozinho
com `prefers-reduced-motion`.

---

**Anterior:** [← 10 — Testes](10-testes.md) · **Próximo:** [12 — Estado ao vivo →](12-estado-ao-vivo.md)
