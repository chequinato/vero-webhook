# 07 — Banco de dados com EF Core

> **🧠 Pra gravar**
> - EF Core é um **ORM**: você escreve LINQ, ele gera SQL.
> - `DbContext` = a sessão com o banco · `DbSet<T>` = uma tabela · migration = versão do esquema.
> - `ValueConverter` criptografa e descriptografa **sem o resto do código perceber**.
> - SQLite em memória some quando a última conexão fecha — por isso existe a conexão "keep-alive".

## O que é

**Entity Framework Core** mapeia classes C# para tabelas SQL:

```csharp
// SQL puro
"SELECT * FROM Transacoes WHERE Status = 'Bloqueada' ORDER BY CreatedAt DESC"

// EF Core — o SQL é gerado
await _context.Transacoes
    .Where(t => t.Status == StatusTransacao.Bloqueada)
    .OrderByDescending(t => t.CreatedAt)
    .ToListAsync();
```

## `DbContext`

```csharp
public class VeroDbContext : DbContext
{
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<TransacaoHistoricoStatus> TransacaoHistoricoStatus => Set<TransacaoHistoricoStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transacao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Valor).HasPrecision(18, 2);         // decimal(18,2)
            entity.Property(e => e.Status).HasConversion<string>();     // enum como texto
            entity.HasOne(e => e.Remetente).WithMany()
                  .HasForeignKey(e => e.RemetenteId)
                  .OnDelete(DeleteBehavior.Restrict);                   // não apaga conta com transação
        });

        modelBuilder.Entity<Conta>().HasData(/* contas iniciais (seed) */);
    }
}
```

Essa forma de configurar com código (em vez de atributos nas entidades) se
chama **Fluent API**. Ela mantém as entidades do Domain limpas de detalhes de
banco.

## Tabela de histórico

Toda mudança de status grava uma linha em `TransacaoHistoricoStatus`: status
anterior, status novo, motivo e quando. É a **trilha de auditoria** — em
sistema financeiro, "quem mudou o quê, de quê para quê e por quê" não é
opcional.

## Migrations

Versionam o esquema do banco, como commits do Git para tabelas:

```bash
dotnet ef migrations add NomeDaMigration --project src/Vero.Infrastructure --startup-project src/Vero.Api
dotnet ef database update --project src/Vero.Infrastructure --startup-project src/Vero.Api
```

O projeto tem duas: `InitialCreate` (as três tabelas + seed) e `AddRiskScore`
(coluna `RiskScore` nullable).

## Criptografia transparente com `ValueConverter`

```csharp
public class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(ICryptoService crypto)
        : base(
            v => crypto.Encrypt(v),     // C# → banco: cifra
            v => crypto.Decrypt(v))     // banco → C#: decifra
    { }
}

// Em OnModelCreating
entity.Property(e => e.NumeroConta).HasConversion(new EncryptedStringConverter(cryptoService));
entity.Property(e => e.Titular).HasConversion(...);
```

O resto da aplicação trabalha com texto puro. A criptografia acontece **dentro**
do EF Core.

O `VeroDbContext` tem dois construtores: com e sem `ICryptoService`. Em
PostgreSQL a criptografia é registrada; no modo `--dev` (SQLite), não.

## PostgreSQL ou SQLite

Escolhido no `Program.cs` e montado em `Infrastructure/DependencyInjection.cs`:

```csharp
if (usarSqlite)
    builder.Services.AddVeroInfrastructureEmMemoria();   // SQLite em memória + seed
else
    builder.Services.AddVeroInfrastructure(connectionString, chaveCriptografia);
```

```csharp
// AddVeroInfrastructureEmMemoria
services.AddDbContext<VeroDbContext>(o => o.UseSqlite("DataSource=VeroDev;Mode=Memory;Cache=Shared"));

var keepAlive = new SqliteConnection("DataSource=VeroDev;Mode=Memory;Cache=Shared");
keepAlive.Open();                       // mantém o banco vivo
services.AddSingleton(keepAlive);
```

**Por que a conexão extra?** SQLite em memória apaga o banco quando a última
conexão fecha. Cada requisição abre e fecha a sua; a conexão "keep-alive"
garante que sempre exista uma aberta.

No modo dev o schema é criado com `EnsureCreatedAsync()` (sem migrations) e o
`DevDataSeeder` popula 6 contas e ~300 transações.

---

**Anterior:** [← 06 — As camadas](06-camadas.md) · **Próximo:** [08 — Segurança →](08-seguranca.md)
