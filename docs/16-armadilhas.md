# 16 — Armadilhas encontradas de verdade

> **🧠 Pra gravar**
> - Efeito React tem que funcionar se rodar **duas vezes seguidas**.
> - Propriedade animada **deixa de ser sua**.
> - `requestAnimationFrame` é para animação, **nunca para correção**.
> - Dependência de efeito compara por **identidade**.
> - Dois campos que precisam concordar são gravados **juntos**.
> - **Fallback silencioso de segredo é pior que falhar ao subir.**
> - Quando "nada acontece", suspeite do **ambiente** antes do código.

Bugs que aconteceram durante o desenvolvimento, com sintoma, causa e lição.
É o material mais caro de produzir e o mais barato de estudar.

---

## Front-end

### 1. `StrictMode` monta o efeito duas vezes

**Sintoma.** Em desenvolvimento, a carga inicial da cena 3D não acontecia.

**Causa.** O React monta, desmonta e remonta cada efeito, de propósito, para
expor limpezas mal feitas. A cena enchia a fila e marcava `jaCarregou = true`;
o React desmontava a cena (e a fila morria com ela); a segunda cena subia vazia
com a bandeira já marcada.

**Correção.** A cena consulta a amostra corrente ao montar, e a limpeza zera a
bandeira:

```typescript
if (!jaCarregou.current && amostraRef.current.length > 0) {
  jaCarregou.current = true;
  for (const tx of amostraRef.current) fila.push(tx);
}
return () => { /* ... */ jaCarregou.current = false; };
```

**Lição.** Estado guardado em `useRef` sobrevive à remontagem — e por isso pode
dessincronizar do recurso que controla. Se a bandeira diz "já fiz isso para
*este* recurso", ela morre junto com o recurso.

### 2. Animação de `opacity` sobrescreve a declaração

**Sintoma.** Uma área declarada com 7% de opacidade aparecia como mancha preta.

**Causa.** Animações têm precedência sobre declarações normais. Com
`animation-fill-mode: both`, o valor final da animação (`opacity: 1`) fica valendo.

**Correção.** Transparência numa propriedade que a animação não toca:
`fill-opacity: .07`.

**Lição.** Propriedade animada deixa de ser sua. Valor base + animação → eixos
diferentes (`opacity` × `fill-opacity`, `transform` no pai × no filho).

### 3. `requestAnimationFrame` estrangulado

**Sintoma.** Todos os contadores presos em zero.

**Causa.** O valor real era agendado num rAF. Com a aba em segundo plano, o
navegador não executa rAF — o número congelava.

**Correção.** Dois gatilhos: rAF para a animação, `setTimeout` como rede de
segurança (veja [11 — Dashboard](11-dashboard-react.md#animar-a-partir-dos-dados)).

**Lição.** rAF é para **animação**, nunca para **correção**.

### 4. Função instável nas dependências do efeito

**Sintoma.** A cena WebGL era destruída e reconstruída o tempo todo.

**Causa.** `[onFiltrar]` nas dependências, e o pai recriava a função a cada render.

**Correção.** A função vai para um `useRef`; o efeito de montagem não depende
dela. O pai também passou a usar `useCallback`.

**Lição.** Dependência compara por **identidade**. Para recurso caro de montar,
pergunte: "este efeito precisa mesmo reagir a isto?"

### 5. Estreitamento de tipo não sobrevive a funções içadas

**Sintoma.** `'canvas' is possibly 'null'` logo depois de uma guarda.

**Causa.** Declarações de função são **içadas** (*hoisted*) e poderiam ser
chamadas antes da guarda; o TypeScript descarta o estreitamento dentro delas.

**Correção.** Um alias tipado depois da guarda:
`const canvas: HTMLCanvasElement = canvasBruto;`

**Lição.** Prefira o alias ao `!`: o `!` cala o compilador; o alias documenta a
invariante e continua verificado.

### 6. Crase dentro de template literal

**Sintoma.** `',' expected` num arquivo aparentemente correto.

**Causa.** Um comentário GLSL dentro de uma template string usava crases — e
crase **encerra** a template string.

**Lição.** Em template literals, crase e `${` são metacaracteres. Vale para
GLSL, SQL, HTML embutidos.

### 7. A ferramenta de verificação também mente

**Sintoma.** A cena parecia congelada.

**Causa.** Não era o código: o painel de visualização estava oculto, e aba
oculta não executa rAF.

**O que fazer.** Verificar o que dá **sem a ferramenta suspeita** (a lógica foi
rodada em Node) e tornar o ambiente observável.

**Lição.** Quando o sintoma é "nada acontece", suspeite do ambiente antes de
reescrever o código.

---

## Backend

### 8. Persistir dois campos em duas idas ao banco

**Evitado no projeto, vale como estudo.** A reavaliação produz status **e**
score. Duas escritas separadas deixariam uma janela em que a linha mostra a
decisão nova com o score velho.

**Correção.** `AtualizarStatusEScoreAsync` grava tudo num `SaveChangesAsync`.

**Lição.** Atomicidade não é só não perder dados — é não expor **estados
intermediários**.

### 9. Regra registrada no lugar errado

**Sintoma.** Nenhum visível — e por isso perigoso.

**Causa.** Toda regra era `IRule`, e cada `Program.cs` decidia quais registrar.
A Api registrava as regras **síncronas** e também o `AnomalyDetectionService`,
que pedia `IEnumerable<IRule>` — ou seja, receberia as regras erradas.

**Correção.** `IRegraSincrona` e `IRegraAssincrona`: cada service pede o tipo
certo, e o compilador garante.

**Lição.** Se a correção depende de "lembrar de registrar certo", transforme em
tipo. **O que o compilador verifica, ninguém esquece.**

### 10. Fallback silencioso de segredo

**Sintoma.** O GitGuardian acusou uma senha no código ao abrir o PR.

**Causa.** A connection string e a chave de criptografia tinham valores
padrão de dev no código (a connection string com a senha do Postgres local
escrita direto num `??`). Pior que a senha
exposta: se a produção esquecesse de configurar a chave, rodaria **em
silêncio** com a chave de dev, pública no repositório.

**Correção.** Nenhum valor padrão. Sem a variável de ambiente, a aplicação lança
uma exceção dizendo o que falta. Um teste garante que o padrão não volta.

**Lição.** **Falhar alto na subida é melhor que funcionar inseguro.** E segredo
que entrou no Git continua no histórico mesmo depois de apagado do código.

### 11. Portas que não conversam

**Sintoma.** API rodando, dashboard e simulador sem conseguir falar com ela.

**Causa.** O `launchSettings.json` subia a API na **5046**; o dashboard e o
simulador esperavam a **5000**.

**Correção.** `launchSettings` na 5000.

**Lição.** Configuração duplicada diverge. Quando dois lugares precisam
concordar num número, um deles precisa ser a fonte — ou os dois precisam ser
verificados juntos.

### 12. Container de DI que não monta

**Evitado no projeto.** Em Development, o .NET valida o container inteiro na
subida (`ValidateOnBuild`). Se o Worker registrasse o `TransacaoService` (que
precisa de ML e SignalR, que o Worker não tem), ele **não subiria**.

**Correção.** Dois métodos: `AddVeroApplication()` (tudo, para a Api) e
`AddVeroAnaliseAssincrona()` (só o que o Worker usa). O `CompositionRootTests`
monta os dois com validação ligada.

**Lição.** Registre só o que o processo usa — e teste a montagem.

### 13. `DateTime` sem valor

**Sintoma.** `ArgumentOutOfRangeException: un-representable DateTime` num teste.

**Causa.** O teste criou uma `Transacao` sem `Timestamp`. O padrão de
`DateTime` é `0001-01-01`; subtrair 5 minutos dele sai do intervalo válido.

**Lição.** Tipo valor nunca é null, mas tem um "zero" que pode ser tão
perigoso quanto. Em teste, preencha o que o código vai usar.

### 14. Duas filas que não se veem

**Sintoma.** Rodando Api e Worker localmente, as transações nunca saem de
`AceitaProvisoria`.

**Causa.** Cada processo tem o seu `InMemoryFilaTransacao`. O que a Api
enfileira fica na memória da Api.

**Lição.** "Em memória" quer dizer **na memória deste processo**. Para
comunicar processos, a fila precisa existir fora deles (SQS, Redis, RabbitMQ).

### 15. Infraestrutura atrás do código

**Estado atual.** O Terraform declara a Lambda com `runtime = "dotnet8"`; o
projeto compila para .NET 10.

**Lição.** Infraestrutura como código também envelhece. Atualizar o framework
inclui atualizar quem vai rodá-lo.

---

**Anterior:** [← 15 — Como rodar](15-como-rodar.md) · **Próximo:** [17 — Hábitos e exercícios →](17-habitos-e-exercicios.md)
