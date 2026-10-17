# Glossário

| Termo | Em uma linha |
|---|---|
| **Adapter** | Classe que veste uma biblioteca externa com a interface que o Domain pediu |
| **AES** | Criptografia simétrica; aqui protege dados sensíveis no banco |
| **Atualização otimista** | Aplicar a mudança localmente antes da confirmação do servidor e reconciliar depois |
| **Change tracking** | O EF Core detecta sozinho o que mudou numa entidade carregada |
| **Code splitting** | Quebrar o bundle em pedaços baixados sob demanda (`import()` + `React.lazy`) |
| **Composição** | Obter comportamento recebendo colaboradores pelo construtor, em vez de herdar |
| **Composition Root** | O único lugar que monta o grafo de objetos (o `Program.cs`) |
| **CORS** | Regra do navegador sobre chamadas entre origens diferentes |
| **Debounce** | Adiar uma ação até os disparos pararem por um intervalo |
| **DI** | Injeção de dependência: receber dependências pelo construtor em vez de criá-las |
| **DLQ** | Dead Letter Queue: fila para mensagens que falharam N vezes |
| **DRY** | *Don't Repeat Yourself*: uma regra, um lugar |
| **DTO** | Objeto que só transporta dados entre a API e o mundo de fora |
| **Entidade** | Objeto do negócio com identidade (`Transacao` tem `Id`) |
| **Feature engineering** | Criar variáveis derivadas para alimentar o modelo de ML |
| **HMAC** | Assinatura com chave secreta que prova autenticidade e integridade |
| **Idempotência** | Aplicar N vezes tem o mesmo efeito que aplicar uma |
| **Imutabilidade** | Não alterar objetos existentes; devolver objetos novos |
| **IV** | Valor aleatório que faz o mesmo texto gerar cifras diferentes |
| **Middleware** | Elo do pipeline HTTP que pode passar a requisição adiante ou barrá-la |
| **Migration** | Versão do esquema do banco, como um commit para tabelas |
| **Mock** | Objeto falso que substitui uma dependência no teste |
| **Observer (pub/sub)** | Produtores publicam eventos; consumidores assinam, sem se conhecerem |
| **ORM** | Mapeia classes para tabelas SQL (EF Core) |
| **Percentil (p50/p90/p99)** | Valor abaixo do qual caem 50%, 90% ou 99% das amostras |
| **Policy** | Regra de decisão pura e isolada (`PoliticaDeRisco`) |
| **Polimorfismo** | Mesma chamada, código diferente conforme o objeto concreto |
| **Pool de objetos** | Reservar objetos na inicialização e reciclá-los, sem alocar no laço |
| **Rate limiting** | Limitar quantas requisições cada cliente faz numa janela de tempo |
| **Repository** | Abstração que faz o banco parecer uma coleção de objetos |
| **Result Pattern** | Devolver o desfecho como valor em vez de lançar exceção |
| **Seed data** | Dados iniciais inseridos ao criar o banco |
| **Shader** | Programa da GPU: *vertex* posiciona, *fragment* colore |
| **SignalR** | Comunicação em tempo real servidor → cliente (WebSocket) |
| **SOLID** | Responsabilidade única, aberto/fechado, Liskov, segregação de interfaces, inversão de dependência |
| **Strategy** | Comportamentos intercambiáveis atrás de uma interface comum |
| **Throttle** | Executar no máximo uma vez a cada intervalo |
| **Timing attack** | Descobrir um segredo medindo quanto tempo uma comparação demora |
| **Typed array** | Array numérico de tamanho fixo (`Float32Array`), que a GPU consome direto |
| **WebGL** | Gráficos acelerados por GPU no navegador |
| **YAGNI** | *You Aren't Gonna Need It*: não crie abstração antes de precisar |

---

**Índice:** [← Guia de estudo](README.md)
