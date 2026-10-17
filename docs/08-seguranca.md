# 08 — Segurança

> **🧠 Pra gravar**
> - **HMAC** prova que quem mandou conhece a chave secreta e que o corpo não foi alterado.
> - Compare assinaturas com **`FixedTimeEquals`**, nunca `==` (timing attack).
> - **AES-256 com IV aleatório**: o mesmo texto cifrado duas vezes dá resultados diferentes.
> - **Rate limiting** com janela deslizante por IP.
> - **Nenhum segredo no código.** Sem a variável de ambiente, a aplicação **não sobe**.

## As camadas de proteção

| Camada | Protege contra | Resposta |
|---|---|---|
| Rate limiting | Flood, força bruta | 429 |
| HMAC | Requisição forjada ou alterada | 401 |
| Replay (id único) | Mesma transação enviada de novo | 409 |
| AES em repouso | Vazamento do banco | — |
| Segredos fora do código | Credencial vazada pelo GitHub | aplicação não sobe |

## HMAC-SHA256 — autenticar o webhook

Quem envia e quem recebe compartilham uma chave secreta:

```
Remetente:  assinatura = HMAC-SHA256(corpo, chave)   → header X-Signature
API:        recalcula HMAC-SHA256(corpo recebido, mesma chave) e compara
```

Se um único caractere do corpo mudar, a assinatura muda. Sem a chave, ninguém
consegue gerar uma assinatura válida.

```csharp
// HmacAuthenticationMiddleware.cs (resumido)
if (!IsWebhookRoute(context)) { await _next(context); return; }   // só POST /transactions

context.Request.EnableBuffering();                 // permite ler o corpo duas vezes
var body = await ReadBodyAsync(context.Request);

var esperada = CalcularHmac(body);                 // Convert.ToHexStringLower(hash)
if (!CompararEmTempoConstante(esperada, recebida))
{
    context.Response.StatusCode = 401;
    return;
}

context.Request.Body.Position = 0;                 // rebobina para o controller ler
await _next(context);
```

**`EnableBuffering` + `Position = 0`:** o corpo de uma requisição é um *stream*
que só pode ser lido uma vez. O middleware precisa ler para validar, e o
controller precisa ler de novo — por isso o buffer e o "rebobinar".

### Comparação em tempo constante

`==` em strings para no **primeiro caractere diferente**. Um atacante mede o
tempo de resposta: se demorou mais, acertou mais caracteres. Repetindo, ele
descobre a assinatura byte a byte — é um **timing attack**.

`CryptographicOperations.FixedTimeEquals` compara **todos** os bytes, sempre
no mesmo tempo.

## AES-256 — criptografia em repouso

`NumeroConta` e `Titular` ficam cifrados no banco (via `ValueConverter`, veja
[07 — Banco de dados](07-banco-de-dados.md)).

```csharp
public AesCryptoService(string encryptionKey)
{
    _key = SHA256.HashData(Encoding.UTF8.GetBytes(encryptionKey));   // sempre 256 bits
}

public string Encrypt(string plainText)
{
    using var aes = Aes.Create();
    aes.Key = _key;
    aes.GenerateIV();                    // IV novo a cada chamada
    aes.Mode = CipherMode.CBC;
    aes.Padding = PaddingMode.PKCS7;
    // ... cifra e devolve Base64(IV + texto cifrado)
}
```

**Por que IV aleatório?** Sem ele, `"conta_123"` sempre viraria o mesmo texto
cifrado, e quem visse o banco saberia quais linhas têm a mesma conta. O IV não
é segredo: vai junto, na frente do texto cifrado, para a decifragem.

## Rate limiting — janela deslizante

```csharp
internal class SlidingWindow
{
    private readonly Queue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    public bool TentarRegistrar(int limite, TimeSpan janela)
    {
        lock (_lock)
        {
            var limiteInferior = DateTime.UtcNow - janela;
            while (_timestamps.Count > 0 && _timestamps.Peek() < limiteInferior)
                _timestamps.Dequeue();               // descarta o que saiu da janela

            if (_timestamps.Count >= limite) return false;

            _timestamps.Enqueue(DateTime.UtcNow);
            return true;
        }
    }
}
```

Uma janela por IP, guardada num `ConcurrentDictionary`. Ao estourar, a
resposta é **429** com o header `Retry-After`.

**Janela fixa × deslizante:** a fixa zera no início de cada minuto, então
permite 60 requisições às 10:00:59 e mais 60 às 10:01:00 — 120 em um segundo.
A deslizante olha sempre os últimos 60 segundos a partir de agora.

Limites em `appsettings.json` (`RateLimiting:MaxRequests`, `WindowSeconds`).

## Segredos fora do código

Três valores são segredos: a **senha do banco**, a **chave HMAC** e a **chave
de criptografia**. Nenhum deles está no código nem no `appsettings.json`:

| Segredo | Variável de ambiente |
|---|---|
| Connection string (com a senha) | `VERO_CONNECTION_STRING` |
| Chave HMAC | `VERO_HMAC_SECRET` |
| Chave AES | `VERO_ENCRYPTION_KEY` |

E não existe valor padrão. Se faltar, a aplicação **se recusa a subir**:

```csharp
// Infrastructure/DependencyInjection.cs
connectionString ??= Environment.GetEnvironmentVariable("VERO_CONNECTION_STRING")
    ?? throw ConfiguracaoAusente("ConnectionStrings:VeroDb", "VERO_CONNECTION_STRING");
```

**Por que falhar em vez de usar um padrão de dev?** Um fallback silencioso faz
a produção rodar com a chave de desenvolvimento, que está pública no GitHub.
Os dados ficariam "criptografados" com uma chave que qualquer um conhece. Falhar
alto na subida é muito melhor que funcionar inseguro em silêncio — veja a
armadilha em [16 — Armadilhas](16-armadilhas.md).

O `.env.example` mostra quais variáveis existem; o `.env` real está no
`.gitignore` e nunca vai para o repositório.

---

**Anterior:** [← 07 — Banco de dados](07-banco-de-dados.md) · **Próximo:** [09 — Machine Learning →](09-machine-learning.md)
