using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vero.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "RiskScore",
                table: "Transacoes",
                type: "real",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "oNEgpnhH5das3LRXY8slT5N8H9f9FMRVnFbmJh5FiZY=", "QbhLH9m+zQSYzw+dboeqAF10wCvKizP34Xg3/o/SF4w=" });

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "BAYZ+eXie8+7sNFYDJ8bl7VD0dF/XyIqi/lwCWErzWg=", "zziBqv30BfWhhn43HCUNlZ8MKBAqsxXHMQ++6Wv97S4=" });

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "OycYXSplE41e2UHHm0wO46kI0G8R0jOnb5mw2bRu6jQ=", "h6LO0Lmo9OZOmdwfQUP6jk8waAGHpxPnjYIibUbdtW8=" });

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "mFBzNi9VduY1WOHDhHwvsiiUj0rwNUh6b6t+tk0ywSs=", "xq6vtPjLOgXgs9P9siQIHj4XNgRVlJB9R0SfgFXqjSw=" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "Transacoes");

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "uecKgu2+3EYaeCwAwPnLCbamRGcwkol1yTOH8JpYqDE=", "mjK5d5tKc64yOSjSWyN5VVZwgUmyYuqKwgp13lJttL0=" });

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "+ri/QrzarFX5/WLGqBLY/s9ZUePf8pw4Csd6mQuLeQI=", "dhb8sXEAGLzFb4aUHkwqC8B/lOTLUCB9GbpWXKJR1L4=" });

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "a49H6w7Hc0b6pGwbMGpOiNZfpWZuh6JkQnUWHu914j8=", "BAWhifphIXq6xGIw7qGAcumcGTGRHvhOHLsl7xP0EnE=" });

            migrationBuilder.UpdateData(
                table: "Contas",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "NumeroConta", "Titular" },
                values: new object[] { "0GhHaC/ixRRv4WVqRbz0DMeoULMBuFMB0WQGPqMARkk=", "+FEEYd9uptV26JP6MfdR19BFQRNBxRWePBIzpUn7dsg=" });
        }
    }
}
