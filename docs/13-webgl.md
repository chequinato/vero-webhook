# 13 — Visualização 3D com WebGL

> **🧠 Pra gravar**
> - **SVG** para dezenas de elementos com texto e clique; **WebGL** quando são centenas em movimento por quadro.
> - A GPU quer **memória contígua** (`Float32Array`), não lista de objetos. Mudou? `needsUpdate = true`.
> - **Nada de alocar dentro do laço de render**: reserve na montagem e recicle (pool).
> - Movimento × `dt` (por segundo, não por quadro), com teto.
> - O que vive na GPU precisa de `dispose()`.

A faixa "00 — Motor de decisão" é uma cena `three.js`. Aqui não existe DOM nem
repintura declarativa: o desempenho é responsabilidade de quem escreve.

## Quando WebGL se justifica

São até 180 partículas com rastro de cinco amostras — 900 pontos por quadro,
mais 64 arestas, com perspectiva. Em SVG seriam ~1.000 nós de DOM repintados
60 vezes por segundo. O critério não é "3D é bonito": é **a quantidade de
elementos por quadro passou do que o DOM aguenta?**

## Os objetos de qualquer cena

```typescript
const renderer = new WebGLRenderer({ canvas, antialias: true, alpha: true });
const cena     = new Scene();
const camera   = new PerspectiveCamera(38, 2, 1, 400);   // fov, aspecto, near, far
const grupo    = new Group();
cena.add(grupo);
```

- **Renderer** desenha; `alpha: true` deixa o canvas transparente e o fundo vem do CSS.
- **Scene** é a árvore de objetos. **Camera** é de onde se olha.
- **Group**: girar a máquina inteira é uma linha.
- **Geometry + Material** viram `Points`, `LineSegments` ou `Mesh`.

## Geometria é array plano

```typescript
const posicoes = new Float32Array(ARESTAS.length * 6);   // 2 vértices × 3 eixos
ARESTAS.forEach(([a, b], k) => {
  const na = NOS[a], nb = NOS[b];
  posicoes.set([na.x, na.y, na.z, nb.x, nb.y, nb.z], k * 6);
});

const g = new BufferGeometry();
g.setAttribute('position', new BufferAttribute(posicoes, 3));

// depois de alterar o conteúdo:
g.getAttribute('position').needsUpdate = true;   // "reenvie para a GPU"
```

Esquecer o `needsUpdate` é o bug clássico: os números mudam e a tela não.

## Shaders

Programas em GLSL que a GPU roda em paralelo:

- **vertex shader** — uma vez por vértice; decide **onde** o ponto cai;
- **fragment shader** — uma vez por pixel; decide **qual cor** ele tem.

| Tipo | Vem de | Varia por |
|---|---|---|
| `attribute` | um `BufferAttribute` | vértice |
| `uniform` | JavaScript, igual para todos | nada (constante no quadro) |
| `varying` | escrito no vertex, lido no fragment | interpolado |

```glsl
// fragment — quadrado de aresta dura
void main() {
  vec2 p = abs(gl_PointCoord - 0.5);
  float d = max(p.x, p.y);
  if (vForma > 0.5 && d < 0.30) discard;   // nó vazado: joga fora o miolo
  gl_FragColor = vec4(vCor, min(1.0, vBrilho));
}
```

O tamanho dos pontos usava uma constante mágica (`240.0 / -mv.z`) acoplada à
distância da câmera, e os nós saíram gigantes. Passar a distância como
`uniform` desacoplou as duas coisas: **número mágico acoplado a outro parâmetro
é bug esperando acontecer**.

## Pool de objetos

```typescript
const ativo  = new Uint8Array(MAX_PARTICULAS);     // slot ocupado?
const tempoP = new Float32Array(MAX_PARTICULAS);   // progresso 0..1

function lancar(tx: TransacaoDto) {
  let i = -1;
  for (let k = 0; k < MAX_PARTICULAS; k++) if (!ativo[k]) { i = k; break; }
  if (i < 0) return;     // pool cheio: o evento já contou nos agregados
  ativo[i] = 1;
  tempoP[i] = 0;
}
```

Alocar dentro do laço gera lixo, e o coletor produz travadas. Com o pool cheio,
a **animação** é descartada — o **dado** não, ele já foi contado. Saber a
diferença é a decisão de projeto.

## Tempo: `dt` com teto

```typescript
const dt = Math.min(0.05, (agora - anterior) / 1000);
tempoP[i] += veloc[i] * dt;      // por segundo, não por quadro
```

Igual a 30, 60 ou 144 Hz. O teto evita que, ao voltar de uma aba em segundo
plano, um `dt` de 30 segundos teleporte todas as partículas.

## Determinismo em vez de `Math.random`

```typescript
function digestao(id: string): number {   // FNV-1a, 32 bits
  let h = 2166136261;
  for (let i = 0; i < id.length; i++) {
    h ^= id.charCodeAt(i);
    h = Math.imul(h, 16777619);           // multiplicação inteira de 32 bits
  }
  return (h >>> 0) / 4294967296;          // 0..1
}
```

A mesma transação desenha sempre o mesmo caminho — o analista reconhece o
traçado, e a cena fica testável. `Math.imul` existe porque `*` em JavaScript é
ponto flutuante de 64 bits e perde os bits altos.

## Limpeza

```typescript
return () => {
  rodando = false;
  desassinar();
  io.disconnect(); ro.disconnect();
  canvas.removeEventListener('pointermove', aoMover);
  gArestas.dispose(); gNos.dispose(); gPart.dispose();
  mArestas.dispose(); mPontos.dispose();
  renderer.dispose();          // libera o contexto WebGL
};
```

Geometrias, materiais e renderer vivem na GPU; o coletor de lixo do JavaScript
**não** os libera. O navegador limita contextos WebGL simultâneos, então o
vazamento vira tela preta.

## Pagar só pelo que aparece

- `IntersectionObserver` pausa a cena quando a faixa sai da tela; `visibilityState` pausa com a aba oculta.
- `setPixelRatio(Math.min(2, devicePixelRatio))`: tela 3× triplicaria o custo.
- Rótulos projetados a cada **dois** quadros — 30 Hz é imperceptível para texto.
- **Code splitting:** `three` tem ~530 kB e não pode atrasar a primeira pintura:

```tsx
const Engine = lazy(() => import('./components/Engine').then(m => ({ default: m.Engine })));

<Suspense fallback={<div className="motor-palco--espera">montando o motor</div>}>
  <Engine /* ... */ />
</Suspense>
```

O `fallback` reserva a mesma altura, para não haver salto de layout.

## Tema dentro do WebGL

A GPU não lê variáveis CSS. A paleta é lida com `getComputedStyle` e reaplicada
quando o tema muda (um `MutationObserver` no atributo `data-mode`). As arestas
são **interpoladas entre o fundo e a tinta**, nunca escurecidas — escurecer
funcionaria no modo escuro e viraria risco preto sobre papel claro.

---

**Anterior:** [← 12 — Estado ao vivo](12-estado-ao-vivo.md) · **Próximo:** [14 — AWS →](14-aws.md)
