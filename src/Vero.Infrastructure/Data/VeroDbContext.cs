using Microsoft.EntityFrameworkCore;
using Vero.Domain.Entities;

namespace Vero.Infrastructure.Data;

public class VeroDbContext : DbContext
{
    public VeroDbContext(DbContextOptions<VeroDbContext> options) : base(options) { }

    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Transacao> Transacoes => Set<Transacao>();
    public DbSet<TransacaoHistoricoStatus> TransacaoHistoricoStatus => Set<TransacaoHistoricoStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Conta
        modelBuilder.Entity<Conta>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NumeroConta).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Titular).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Score).IsRequired();
            entity.HasIndex(e => e.NumeroConta).IsUnique();
        });

        // Transacao
        modelBuilder.Entity<Transacao>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100);
            entity.Property(e => e.Valor).HasPrecision(18, 2);
            entity.Property(e => e.Tipo).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Moeda).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Status).IsRequired()
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.Property(e => e.Motivo).HasMaxLength(200);

            entity.HasOne(e => e.Remetente)
                .WithMany()
                .HasForeignKey(e => e.RemetenteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Destinatario)
                .WithMany()
                .HasForeignKey(e => e.DestinatarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TransacaoHistoricoStatus
        modelBuilder.Entity<TransacaoHistoricoStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TransacaoId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.StatusAnterior).HasMaxLength(30);
            entity.Property(e => e.StatusNovo).IsRequired().HasMaxLength(30);
            entity.Property(e => e.Motivo).HasMaxLength(200);

            entity.HasOne(e => e.Transacao)
                .WithMany()
                .HasForeignKey(e => e.TransacaoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed de contas para desenvolvimento
        modelBuilder.Entity<Conta>().HasData(
            new Conta { Id = 1, NumeroConta = "conta_123", Titular = "Maria Silva", Score = 85, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Conta { Id = 2, NumeroConta = "conta_456", Titular = "João Santos", Score = 72, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Conta { Id = 3, NumeroConta = "conta_789", Titular = "Ana Oliveira", Score = 25, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Conta { Id = 4, NumeroConta = "conta_000", Titular = "Carlos Ferreira", Score = 50, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
