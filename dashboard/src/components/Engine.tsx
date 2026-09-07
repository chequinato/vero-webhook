import { useEffect, useRef, useState } from 'react';
import {
  BufferAttribute,
  BufferGeometry,
  Color,
  Group,
  LineBasicMaterial,
  LineSegments,
  PerspectiveCamera,
  Points,
  Scene,
  ShaderMaterial,
  Vector3,
  WebGLRenderer,
} from 'three';
import { assinar } from '../engine/bus';
import { ARESTAS, ESTAGIOS, NOS, estadoDaRota, rotear } from '../engine/topologia';
import type { TransacaoDto } from '../types';

/**
 * 00 — MOTOR DE DECISÃO
 *
 * O palco da folha. Não é um gráfico do que aconteceu: é a máquina rodando.
 * Cada ponto que atravessa a malha é uma transação de verdade, e o portão em
 * que ela aterrissa é a decisão que o backend tomou sobre ela.
 *
 * A malha é o pipeline real do Vero — barramento, regras síncronas, as sete
 * features do FastTree, regras assíncronas, decisão. Ver `engine/topologia.ts`
 * para de onde vem cada nome, e para a ressalva honesta sobre o trajeto pelo
 * miolo (o desfecho é real; a atribuição por feature, a API não entrega).
 *
 * ── Por que WebGL e não SVG ──
 * Centenas de pontos em movimento com rastro, num espaço com profundidade
 * real, é trabalho de GPU. Em SVG seriam milhares de nós de DOM repintados a
 * cada quadro, e o resto do painel travaria junto.
 *
 * ── Por que não brilha ──
 * Blending normal, arestas duras, zero bloom. A cena é um esquema técnico em
 * três dimensões, não uma tela de proteção — a mesma disciplina de cor do
 * resto da folha: quatro sinais, e só nos portões de saída.
 */

interface Props {
  /** Amostra já carregada — atravessa a máquina uma vez, na abertura. */
  amostra: TransacaoDto[];
  conectado: boolean;
  /** Clicar num portão filtra o registro por aquele estado. */
  onFiltrar: (status: string) => void;
}

const MAX_PARTICULAS = 180;
const TRILHA = 5;
const SEGMENTOS = 4;

const VERT = `
  attribute float aTamanho;
  attribute float aBrilho;
  attribute float aForma;
  attribute vec3 aCor;
  uniform float uDpr;
  uniform float uDist;
  varying float vBrilho;
  varying float vForma;
  varying vec3 vCor;
  void main() {
    vBrilho = aBrilho;
    vForma = aForma;
    vCor = aCor;
    vec4 mv = modelViewMatrix * vec4(position, 1.0);
    // aTamanho é o tamanho em pixels na distancia nominal da camera; a
    // razao com a profundidade real da a perspectiva sem descolar a
    // escala do enquadramento.
    gl_PointSize = aTamanho * uDpr * (uDist / max(1.0, -mv.z));
    gl_Position = projectionMatrix * mv;
  }
`;

const FRAG = `
  varying float vBrilho;
  varying float vForma;
  varying vec3 vCor;
  void main() {
    if (vBrilho <= 0.004) discard;
    // Quadrado de aresta dura: nada de disco desfocado.
    vec2 p = abs(gl_PointCoord - 0.5);
    float d = max(p.x, p.y);
    if (vForma > 0.5 && d < 0.30) discard;   // nó vazado
    gl_FragColor = vec4(vCor, min(1.0, vBrilho));
  }
`;

function lerCor(estilo: CSSStyleDeclaration, nome: string, alternativa: string): Color {
  const bruto = estilo.getPropertyValue(nome).trim();
  const c = new Color();
  try {
    c.setStyle(bruto || alternativa);
  } catch {
    c.setStyle(alternativa);
  }
  return c;
}

interface Contadores {
  aprovada: number;
  suspeita: number;
  bloqueada: number;
  aceitaprovisoria: number;
}

const VAZIO: Contadores = { aprovada: 0, suspeita: 0, bloqueada: 0, aceitaprovisoria: 0 };

const ROTULO_ESTADO: Record<string, string> = {
  aprovada: 'aprovada',
  suspeita: 'suspeita',
  bloqueada: 'bloqueada',
  aceitaprovisoria: 'em análise',
};

export function Engine({ amostra, conectado, onFiltrar }: Props) {
  const caixaRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const rotulosRef = useRef<HTMLDivElement>(null);
  const portoesRef = useRef<HTMLDivElement>(null);

  const [suportado, setSuportado] = useState(true);
  const [contagem, setContagem] = useState<Contadores>(VAZIO);
  const [transito, setTransito] = useState(0);
  const [ultima, setUltima] = useState<{ status: string; valor: number; risco: number } | null>(null);
  const [sobre, setSobre] = useState<{ lbl: string; n: number } | null>(null);

  // A carga inicial só pode acontecer uma vez, mesmo que a amostra
  // seja reescrita a cada reconciliação com o servidor.
  const jaCarregou = useRef(false);
  const enfileirarRef = useRef<((tx: TransacaoDto, imediato: boolean) => void) | null>(null);
  const amostraRef = useRef<TransacaoDto[]>(amostra);
  amostraRef.current = amostra;

  // O retorno vive numa referência: se entrasse nas dependências do efeito,
  // qualquer repintura do painel derrubaria e reconstruiria a cena inteira.
  const filtrarRef = useRef(onFiltrar);
  useEffect(() => {
    filtrarRef.current = onFiltrar;
  }, [onFiltrar]);

  useEffect(() => {
    const caixaBruta = caixaRef.current;
    const canvasBruto = canvasRef.current;
    if (!caixaBruta || !canvasBruto) return;

    const caixa: HTMLDivElement = caixaBruta;
    const canvas: HTMLCanvasElement = canvasBruto;

    let renderer: WebGLRenderer;
    try {
      renderer = new WebGLRenderer({ canvas, antialias: true, alpha: true });
    } catch {
      setSuportado(false);
      return;
    }
    renderer.setClearColor(0x000000, 0);

    const calmo = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const cena = new Scene();
    const camera = new PerspectiveCamera(38, 2, 1, 400);
    const grupo = new Group();
    cena.add(grupo);

    // ── Paleta: lida do CSS, para o modo tinta continuar valendo ──
    const paleta = {
      fundo: new Color(),
      fio: new Color(),
      no: new Color(),
      aprovada: new Color(),
      suspeita: new Color(),
      bloqueada: new Color(),
      aceitaprovisoria: new Color(),
    };

    function lerPaleta() {
      const s = getComputedStyle(caixa);
      paleta.fundo = lerCor(s, '--zone', '#14120f');
      paleta.fio = lerCor(s, '--zone-rule', '#302d27');
      paleta.no = lerCor(s, '--zone-fg', '#e8e4d9');
      paleta.aprovada = lerCor(s, '--zone-verde', '#3fb183');
      paleta.suspeita = lerCor(s, '--zone-amber', '#dda01a');
      paleta.bloqueada = lerCor(s, '--zone-signal', '#ff4e24');
      paleta.aceitaprovisoria = lerCor(s, '--zone-azul', '#6f9fea');
    }
    lerPaleta();

    const corDoEstado = (chave: string): Color =>
      chave === 'aprovada' ? paleta.aprovada
        : chave === 'suspeita' ? paleta.suspeita
        : chave === 'bloqueada' ? paleta.bloqueada
        : paleta.aceitaprovisoria;

    // ══ Arestas ══
    const gArestas = new BufferGeometry();
    const posA = new Float32Array(ARESTAS.length * 6);
    const corA = new Float32Array(ARESTAS.length * 6);
    ARESTAS.forEach(([a, b], k) => {
      const na = NOS[a];
      const nb = NOS[b];
      posA.set([na.x, na.y, na.z, nb.x, nb.y, nb.z], k * 6);
    });
    gArestas.setAttribute('position', new BufferAttribute(posA, 3));
    gArestas.setAttribute('color', new BufferAttribute(corA, 3));
    const mArestas = new LineBasicMaterial({ vertexColors: true });
    grupo.add(new LineSegments(gArestas, mArestas));

    /**
     * Fios recuados no eixo Z desbotam — é o que dá volume à malha. A cor
     * é interpolada entre o fundo da faixa e a tinta, e não escurecida:
     * escurecer funcionaria no modo papel e viraria risco preto sobre papel
     * claro no modo tinta.
     */
    const mistura = new Color();
    function pintarArestas(destacado: number) {
      ARESTAS.forEach(([a, b], k) => {
        const toca = destacado >= 0 && (a === destacado || b === destacado);
        for (const [idx, no] of [[0, NOS[a]], [1, NOS[b]]] as const) {
          const prof = 1 - Math.min(1, Math.abs(no.z) / 11) * 0.55;
          const f = (toca ? 0.8 : 0.24) * prof;
          mistura.copy(paleta.fundo).lerp(paleta.no, f);
          corA.set([mistura.r, mistura.g, mistura.b], k * 6 + idx * 3);
        }
      });
      gArestas.getAttribute('color').needsUpdate = true;
    }

    // ══ Nós ══
    const nN = NOS.length;
    const gNos = new BufferGeometry();
    const posN = new Float32Array(nN * 3);
    const corN = new Float32Array(nN * 3);
    const tamN = new Float32Array(nN);
    const briN = new Float32Array(nN);
    const formaN = new Float32Array(nN);
    const pulso = new Float32Array(nN);
    const passaram = new Int32Array(nN);

    NOS.forEach((n, i) => {
      posN.set([n.x, n.y, n.z], i * 3);
      tamN[i] = n.estagio === 4 ? 13 : n.estagio === 2 ? 9 : 8;
      formaN[i] = n.estagio === 4 ? 1 : 0; // portões são vazados
    });
    gNos.setAttribute('position', new BufferAttribute(posN, 3));
    gNos.setAttribute('aCor', new BufferAttribute(corN, 3));
    gNos.setAttribute('aTamanho', new BufferAttribute(tamN, 1));
    gNos.setAttribute('aBrilho', new BufferAttribute(briN, 1));
    gNos.setAttribute('aForma', new BufferAttribute(formaN, 1));

    const mPontos = new ShaderMaterial({
      vertexShader: VERT,
      fragmentShader: FRAG,
      transparent: true,
      depthWrite: false,
      uniforms: {
        uDpr: { value: Math.min(2, window.devicePixelRatio || 1) },
        uDist: { value: 40 },
      },
    });
    grupo.add(new Points(gNos, mPontos));

    // ══ Partículas (com rastro) ══
    const nP = MAX_PARTICULAS * TRILHA;
    const gPart = new BufferGeometry();
    const posP = new Float32Array(nP * 3);
    const corP = new Float32Array(nP * 3);
    const tamP = new Float32Array(nP);
    const briP = new Float32Array(nP);
    const formaP = new Float32Array(nP);
    gPart.setAttribute('position', new BufferAttribute(posP, 3));
    gPart.setAttribute('aCor', new BufferAttribute(corP, 3));
    gPart.setAttribute('aTamanho', new BufferAttribute(tamP, 1));
    gPart.setAttribute('aBrilho', new BufferAttribute(briP, 1));
    gPart.setAttribute('aForma', new BufferAttribute(formaP, 1));
    grupo.add(new Points(gPart, mPontos));

    const ativo = new Uint8Array(MAX_PARTICULAS);
    const tempoP = new Float32Array(MAX_PARTICULAS);
    const veloc = new Float32Array(MAX_PARTICULAS);
    const trechoP = new Int8Array(MAX_PARTICULAS);
    const rotas = new Int32Array(MAX_PARTICULAS * (SEGMENTOS + 1));
    const corFinal = new Float32Array(MAX_PARTICULAS * 3);
    const destino = new Int32Array(MAX_PARTICULAS);
    const valorP = new Float32Array(MAX_PARTICULAS);
    const riscoP = new Float32Array(MAX_PARTICULAS);

    const contadores: Contadores = { ...VAZIO };
    let emTransito = 0;
    let ultimaDecisao: { status: string; valor: number; risco: number } | null = null;

    const fila: TransacaoDto[] = [];

    function lancar(tx: TransacaoDto) {
      let i = -1;
      for (let k = 0; k < MAX_PARTICULAS; k++) {
        if (!ativo[k]) { i = k; break; }
      }
      if (i < 0) return; // pool cheio: o evento já contou nos agregados

      const rota = rotear(tx);
      rotas.set(rota, i * (SEGMENTOS + 1));
      ativo[i] = 1;
      tempoP[i] = 0;
      trechoP[i] = -1;
      veloc[i] = 0.19 + Math.min(0.12, (tx.riskScore ?? 0.2) * 0.14);
      destino[i] = rota[rota.length - 1];
      valorP[i] = tx.valor;
      riscoP[i] = tx.riskScore ?? 0;

      const c = corDoEstado(estadoDaRota(rota));
      corFinal.set([c.r, c.g, c.b], i * 3);
      emTransito++;
    }

    function aterrissar(i: number) {
      const j = destino[i];
      pulso[j] = 1.9;
      passaram[j]++;
      const chave = NOS[j].status ?? 'aceitaprovisoria';
      contadores[chave as keyof Contadores]++;
      ultimaDecisao = { status: chave, valor: valorP[i], risco: riscoP[i] };
      ativo[i] = 0;
      emTransito = Math.max(0, emTransito - 1);
    }

    enfileirarRef.current = (tx, imediato) => {
      if (imediato) lancar(tx);
      else fila.push(tx);
    };

    // Se a amostra já chegou antes desta cena existir (remontagem em
    // desenvolvimento, ou troca de tema), ela entra na fila agora.
    if (!jaCarregou.current && amostraRef.current.length > 0) {
      jaCarregou.current = true;
      for (const tx of amostraRef.current) fila.push(tx);
    }

    // ── Amostragem ao longo da rota: trechos com aceleração mecânica ──
    const p0 = new Vector3();
    const p1 = new Vector3();
    function amostrar(i: number, t: number, saida: Vector3) {
      const u = Math.max(0, Math.min(1, t)) * SEGMENTOS;
      const seg = Math.min(SEGMENTOS - 1, Math.floor(u));
      const f = u - seg;
      const e = f * f * (3 - 2 * f); // desacelera ao chegar em cada nó
      const base = i * (SEGMENTOS + 1);
      const a = NOS[rotas[base + seg]];
      const b = NOS[rotas[base + seg + 1]];
      p0.set(a.x, a.y, a.z);
      p1.set(b.x, b.y, b.z);
      saida.copy(p0).lerp(p1, e);
      return seg;
    }

    // ══ Câmera, ponteiro, enquadramento ══
    let larg = 1;
    let alt = 1;
    const mouse = { x: 0, y: 0, alvoX: 0, alvoY: 0, dentro: false, px: -999, py: -999 };

    function enquadrar() {
      const r = caixa.getBoundingClientRect();
      larg = Math.max(320, r.width);
      alt = Math.max(240, r.height);
      renderer.setPixelRatio(Math.min(2, window.devicePixelRatio || 1));
      renderer.setSize(larg, alt, false);
      camera.aspect = larg / alt;

      // Enquadra a máquina inteira, seja qual for a proporção da faixa.
      const fov = (camera.fov * Math.PI) / 180;
      const porAltura = 25 / (2 * Math.tan(fov / 2));
      const porLargura = 64 / camera.aspect / (2 * Math.tan(fov / 2));
      camera.position.set(0, 0, Math.max(porAltura, porLargura));
      camera.lookAt(0, 0, 0);
      camera.updateProjectionMatrix();
      mPontos.uniforms.uDpr.value = Math.min(2, window.devicePixelRatio || 1);
      mPontos.uniforms.uDist.value = camera.position.z;
    }
    enquadrar();

    const ro = new ResizeObserver(enquadrar);
    ro.observe(caixa);

    function aoMover(ev: PointerEvent) {
      const r = caixa.getBoundingClientRect();
      mouse.px = ev.clientX - r.left;
      mouse.py = ev.clientY - r.top;
      mouse.alvoX = (mouse.px / r.width - 0.5) * 2;
      mouse.alvoY = (mouse.py / r.height - 0.5) * 2;
      mouse.dentro = true;
    }
    function aoSair() {
      mouse.dentro = false;
      mouse.alvoX = 0;
      mouse.alvoY = 0;
      mouse.px = -999;
      mouse.py = -999;
    }
    canvas.addEventListener('pointermove', aoMover);
    canvas.addEventListener('pointerleave', aoSair);

    let destacado = -1;
    function aoClicar() {
      if (destacado >= 0 && NOS[destacado].status) filtrarRef.current(NOS[destacado].status!);
    }
    canvas.addEventListener('click', aoClicar);

    pintarArestas(-1);

    // ══ Laço ══
    const tela = new Vector3();
    const pos = new Vector3();
    const projetados: Array<{ x: number; y: number }> = NOS.map(() => ({ x: 0, y: 0 }));

    let rodando = true;
    let visivel = true;
    let anterior = performance.now();
    let relogio = 0;
    let despejo = 0;
    let quadro = 0;

    const io = new IntersectionObserver(([e]) => { visivel = e.isIntersecting; }, { threshold: 0.01 });
    io.observe(caixa);

    function passo(agora: number) {
      if (!rodando) return;
      requestAnimationFrame(passo);

      const dt = Math.min(0.05, (agora - anterior) / 1000);
      anterior = agora;
      if (!visivel || document.visibilityState !== 'visible') return;
      relogio += dt;

      // Fila da carga inicial: uma a cada ~65ms, para a máquina encher.
      despejo += dt;
      while (fila.length > 0 && despejo > 0.065) {
        despejo -= 0.065;
        lancar(fila.shift()!);
      }

      // ── Orientação: deriva lenta + paralaxe do ponteiro ──
      if (!calmo) {
        mouse.x += (mouse.alvoX - mouse.x) * Math.min(1, dt * 4);
        mouse.y += (mouse.alvoY - mouse.y) * Math.min(1, dt * 4);
        grupo.rotation.y = -0.3 + Math.sin(relogio * 0.09) * 0.1 + mouse.x * 0.34;
        grupo.rotation.x = Math.sin(relogio * 0.07) * 0.045 + mouse.y * 0.16;
      } else {
        grupo.rotation.set(0.02, -0.3, 0);
      }
      grupo.updateMatrixWorld();

      // ── Nós: respiração de base + pulso decrescente ──
      for (let i = 0; i < nN; i++) {
        pulso[i] *= Math.pow(0.02, dt);
        const onda = 0.42 + 0.16 * Math.sin(relogio * 0.9 - NOS[i].x * 0.07);
        const b = Math.min(1.6, onda + pulso[i]);
        briN[i] = destacado === i ? Math.max(b, 1.2) : b;
        const c = NOS[i].status ? corDoEstado(NOS[i].status!) : paleta.no;
        corN.set([c.r, c.g, c.b], i * 3);
      }
      gNos.getAttribute('aBrilho').needsUpdate = true;
      gNos.getAttribute('aCor').needsUpdate = true;

      // ── Partículas ──
      briP.fill(0);
      for (let i = 0; i < MAX_PARTICULAS; i++) {
        if (!ativo[i]) continue;
        tempoP[i] += veloc[i] * dt;

        if (tempoP[i] >= 1) { aterrissar(i); continue; }

        const seg = amostrar(i, tempoP[i], pos);
        if (seg > trechoP[i]) {
          trechoP[i] = seg;
          const no = rotas[i * (SEGMENTOS + 1) + seg];
          pulso[no] = Math.max(pulso[no], 1.1);
          passaram[no]++;
        }

        // A cor só troca no último trecho: a decisão acontece em cena.
        const decidiu = tempoP[i] > 0.76;
        const cr = decidiu ? corFinal[i * 3] : paleta.no.r;
        const cg = decidiu ? corFinal[i * 3 + 1] : paleta.no.g;
        const cb = decidiu ? corFinal[i * 3 + 2] : paleta.no.b;

        for (let k = 0; k < TRILHA; k++) {
          const idx = i * TRILHA + k;
          const tk = tempoP[i] - k * 0.016;
          if (tk < 0) { briP[idx] = 0; continue; }
          amostrar(i, tk, tela);
          posP.set([tela.x, tela.y, tela.z], idx * 3);
          corP.set([cr, cg, cb], idx * 3);
          tamP[idx] = (k === 0 ? 11 : 7) * (1 - (k * 0.7) / (TRILHA + 1));
          briP[idx] = (k === 0 ? 1 : 0.42) * (1 - k / TRILHA);
        }
      }
      gPart.getAttribute('position').needsUpdate = true;
      gPart.getAttribute('aCor').needsUpdate = true;
      gPart.getAttribute('aTamanho').needsUpdate = true;
      gPart.getAttribute('aBrilho').needsUpdate = true;

      renderer.render(cena, camera);

      // ── Projeções (rótulos, portões, alvo do cursor) a cada 2 quadros ──
      quadro++;
      if (quadro % 2 === 0) {
        for (let i = 0; i < nN; i++) {
          tela.set(NOS[i].x, NOS[i].y, NOS[i].z).applyMatrix4(grupo.matrixWorld).project(camera);
          projetados[i].x = (tela.x * 0.5 + 0.5) * larg;
          projetados[i].y = (-tela.y * 0.5 + 0.5) * alt;
        }

        let novo = -1;
        if (mouse.dentro) {
          let melhor = 26 * 26;
          for (let i = 0; i < nN; i++) {
            const dx = projetados[i].x - mouse.px;
            const dy = projetados[i].y - mouse.py;
            const d = dx * dx + dy * dy;
            if (d < melhor) { melhor = d; novo = i; }
          }
        }
        if (novo !== destacado) {
          destacado = novo;
          pintarArestas(destacado);
          canvas.style.cursor = destacado >= 0 && NOS[destacado].status ? 'pointer' : 'default';
          setSobre(destacado >= 0 ? { lbl: NOS[destacado].lbl, n: passaram[destacado] } : null);
        }

        // Rótulos de estágio: ancorados no nó mais alto de cada estágio.
        const cont = rotulosRef.current;
        if (cont) {
          for (let e = 0; e < ESTAGIOS.length; e++) {
            const el = cont.children[e] as HTMLElement | undefined;
            if (!el) continue;
            let topo = Infinity;
            let cx = 0;
            for (let i = 0; i < nN; i++) {
              if (NOS[i].estagio !== e) continue;
              if (projetados[i].y < topo) { topo = projetados[i].y; cx = projetados[i].x; }
            }
            el.style.transform = `translate(-50%, 0) translate(${cx.toFixed(1)}px, ${(topo - 30).toFixed(1)}px)`;
          }
        }

        // Contadores de portão: colados na coluna de decisão.
        const pcont = portoesRef.current;
        if (pcont) {
          const portoes = NOS.map((n, i) => ({ n, i })).filter(({ n }) => n.estagio === 4);
          portoes.forEach(({ i }, k) => {
            const el = pcont.children[k] as HTMLElement | undefined;
            if (!el) return;
            el.style.transform = `translate(0, -50%) translate(${(projetados[i].x + 22).toFixed(1)}px, ${projetados[i].y.toFixed(1)}px)`;
          });
        }
      }
    }
    requestAnimationFrame(passo);

    // Despejo dos contadores para o React: quatro vezes por segundo basta,
    // e evita uma repintura da árvore por partícula.
    const bombeamento = setInterval(() => {
      setContagem({ ...contadores });
      setTransito(emTransito);
      if (ultimaDecisao) setUltima(ultimaDecisao);
    }, 250);

    const observadorTema = new MutationObserver(() => {
      lerPaleta();
      pintarArestas(destacado);
    });
    observadorTema.observe(document.documentElement, { attributes: true, attributeFilter: ['data-mode'] });

    const desassinar = assinar(tx => lancar(tx));

    return () => {
      rodando = false;
      clearInterval(bombeamento);
      desassinar();
      observadorTema.disconnect();
      io.disconnect();
      ro.disconnect();
      canvas.removeEventListener('pointermove', aoMover);
      canvas.removeEventListener('pointerleave', aoSair);
      canvas.removeEventListener('click', aoClicar);
      gArestas.dispose();
      gNos.dispose();
      gPart.dispose();
      mArestas.dispose();
      mPontos.dispose();
      renderer.dispose();
      enfileirarRef.current = null;
      // A cena morreu e levou a fila: a próxima tem direito à carga de novo.
      jaCarregou.current = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── Carga inicial: a amostra já buscada atravessa a máquina uma vez ──
  useEffect(() => {
    if (jaCarregou.current || amostra.length === 0) return;
    const enfileirar = enfileirarRef.current;
    if (!enfileirar) return;
    jaCarregou.current = true;
    for (const tx of amostra) enfileirar(tx, false);
  }, [amostra]);

  const decididas =
    contagem.aprovada + contagem.suspeita + contagem.bloqueada + contagem.aceitaprovisoria;

  return (
    <section className="motor">
      <div className="motor-head">
        <span>
          <span className="idx">00</span>{' '}
          <span className="name">Motor de decisão — ao vivo</span>
        </span>
        <span className="band-legend">
          <span>{conectado ? 'barramento aberto' : 'barramento mudo'}</span>
          <span className="motor-ressalva">
            desfecho e escore reais · trajeto pelo miolo é ilustrativo
          </span>
        </span>
      </div>

      <div className="motor-palco" ref={caixaRef}>
        <canvas ref={canvasRef} className="motor-tela" />

        {!suportado && (
          <div className="motor-sem-webgl">
            WebGL indisponível neste navegador — as demais seções seguem funcionando
          </div>
        )}

        {/* Rótulos de estágio, posicionados por projeção da cena */}
        <div className="motor-rotulos" ref={rotulosRef} aria-hidden="true">
          {ESTAGIOS.map(e => (
            <span className="motor-estagio" key={e.id}>
              {e.rotulo}
            </span>
          ))}
        </div>

        {/* Contadores colados na coluna de decisão */}
        <div className="motor-portoes" ref={portoesRef}>
          {(['aprovada', 'aceitaprovisoria', 'suspeita', 'bloqueada'] as const).map(k => (
            <button
              key={k}
              className={`motor-portao motor-portao--${k}`}
              onClick={() => onFiltrar(k)}
              title={`Filtrar o registro por ${ROTULO_ESTADO[k]}`}
            >
              <b>{contagem[k]}</b>
              <span>{ROTULO_ESTADO[k]}</span>
            </button>
          ))}
        </div>

        {/* Leitura de estado */}
        <div className="motor-hud">
          <span>
            em trânsito <b>{String(transito).padStart(2, '0')}</b>
          </span>
          <span>
            decididas nesta sessão <b>{decididas}</b>
          </span>
          {ultima && (
            <span className={`motor-ultima motor-ultima--${ultima.status}`}>
              última <b>{ROTULO_ESTADO[ultima.status]}</b>
            </span>
          )}
        </div>

        {sobre && (
          <div className="motor-sonda">
            <b>{sobre.lbl}</b>
            <span>{sobre.n} passagens</span>
          </div>
        )}
      </div>
    </section>
  );
}
