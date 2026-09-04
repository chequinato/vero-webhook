using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vero.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Contas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NumeroConta = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Titular = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transacoes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RemetenteId = table.Column<int>(type: "integer", nullable: false),
                    DestinatarioId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Moeda = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transacoes_Contas_DestinatarioId",
                        column: x => x.DestinatarioId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transacoes_Contas_RemetenteId",
                        column: x => x.RemetenteId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransacaoHistoricoStatus",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransacaoId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StatusAnterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    StatusNovo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AlteradoEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransacaoHistoricoStatus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransacaoHistoricoStatus_Transacoes_TransacaoId",
                        column: x => x.TransacaoId,
                        principalTable: "Transacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Contas",
                columns: new[] { "Id", "CreatedAt", "NumeroConta", "Score", "Titular" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "8ipLMMnsPFleUqirCP9TXqBt37XiypWoDlVJ0obmP20=", 85, "o6mrJTEQhFzyR+VyT215A3VJwsuFnhGsn/8FNo0LEWI=" },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "7cWfGdHbW9xwenLvEoVBVrSqtphb93BXolGq+bHmMtg=", 72, "FPd6bEXB3Z4rmoijHNXz6jiPxm+3japONA5sMp76VyY=" },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "v5PKWxG1GvbUvkPXEx8/FQP4xEiVDKEFm5LPfjhAHcs=", 25, "NuGvSzO4vOvoRQCvXjLk/v4gzYezbquyb5GitdoJOdg=" },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "pxo+64T4BeI7404xq+Y9rDvgWhgmgYZCSnxjm2SABI8=", 50, "5mgShdApHVNs60gLaC17Tc2RfIO+uhJKh3r74JJdG8A=" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contas_NumeroConta",
                table: "Contas",
                column: "NumeroConta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransacaoHistoricoStatus_TransacaoId",
                table: "TransacaoHistoricoStatus",
                column: "TransacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_DestinatarioId",
                table: "Transacoes",
                column: "DestinatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_RemetenteId",
                table: "Transacoes",
                column: "RemetenteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransacaoHistoricoStatus");

            migrationBuilder.DropTable(
                name: "Transacoes");

            migrationBuilder.DropTable(
                name: "Contas");
        }
    }
}
