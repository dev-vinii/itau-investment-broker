using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ItauInvestmentBroker.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchemaWithBusinessConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cestas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Ativa = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DataCriacao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DataDesativacao = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ativa_unica = table.Column<int>(type: "int", nullable: true, computedColumnSql: "CASE WHEN `Ativa` = 1 THEN 1 ELSE NULL END", stored: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cestas", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "clientes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Cpf = table.Column<string>(type: "varchar(14)", maxLength: 14, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValorMensal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    DataAdesao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clientes", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "itens_cesta",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ticker = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Percentual = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CestaId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_cesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_cesta_cestas_CestaId",
                        column: x => x.CestaId,
                        principalTable: "cestas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "contas_graficas",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    NumeroConta = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClienteId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contas_graficas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contas_graficas_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "historico_valor_mensal",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClienteId = table.Column<long>(type: "bigint", nullable: false),
                    ValorAnterior = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorNovo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataAlteracao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_valor_mensal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_historico_valor_mensal_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "vendas_rebalanceamento",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ClienteId = table.Column<long>(type: "bigint", nullable: false),
                    Ticker = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    PrecoVenda = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PrecoMedio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ValorVenda = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Lucro = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataVenda = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vendas_rebalanceamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vendas_rebalanceamento_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "custodias",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ticker = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    PrecoMedio = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DataUltimaAtualizacao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ContaGraficaId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custodias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_custodias_contas_graficas_ContaGraficaId",
                        column: x => x.ContaGraficaId,
                        principalTable: "contas_graficas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ordens_compra",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DataExecucao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ContaGraficaId = table.Column<long>(type: "bigint", nullable: false),
                    CestaId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordens_compra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ordens_compra_cestas_CestaId",
                        column: x => x.CestaId,
                        principalTable: "cestas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ordens_compra_contas_graficas_ContaGraficaId",
                        column: x => x.ContaGraficaId,
                        principalTable: "contas_graficas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "distribuicoes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DataDistribuicao = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OrdemCompraId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_distribuicoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_distribuicoes_ordens_compra_OrdemCompraId",
                        column: x => x.OrdemCompraId,
                        principalTable: "ordens_compra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "itens_ordem_compra",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ticker = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TipoMercado = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OrdemCompraId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_ordem_compra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_ordem_compra_ordens_compra_OrdemCompraId",
                        column: x => x.OrdemCompraId,
                        principalTable: "ordens_compra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "itens_distribuicao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ticker = table.Column<string>(type: "varchar(12)", maxLength: 12, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    DistribuicaoId = table.Column<long>(type: "bigint", nullable: false),
                    ContaGraficaId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_itens_distribuicao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itens_distribuicao_contas_graficas_ContaGraficaId",
                        column: x => x.ContaGraficaId,
                        principalTable: "contas_graficas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_itens_distribuicao_distribuicoes_DistribuicaoId",
                        column: x => x.DistribuicaoId,
                        principalTable: "distribuicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_cestas_ativa_unica",
                table: "cestas",
                column: "ativa_unica",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clientes_Cpf",
                table: "clientes",
                column: "Cpf",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_graficas_ClienteId",
                table: "contas_graficas",
                column: "ClienteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contas_graficas_NumeroConta",
                table: "contas_graficas",
                column: "NumeroConta",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_custodias_ContaGraficaId",
                table: "custodias",
                column: "ContaGraficaId");

            migrationBuilder.CreateIndex(
                name: "IX_distribuicoes_OrdemCompraId",
                table: "distribuicoes",
                column: "OrdemCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_historico_valor_mensal_ClienteId",
                table: "historico_valor_mensal",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_cesta_CestaId",
                table: "itens_cesta",
                column: "CestaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_distribuicao_ContaGraficaId",
                table: "itens_distribuicao",
                column: "ContaGraficaId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_distribuicao_DistribuicaoId",
                table: "itens_distribuicao",
                column: "DistribuicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_itens_ordem_compra_OrdemCompraId",
                table: "itens_ordem_compra",
                column: "OrdemCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_CestaId",
                table: "ordens_compra",
                column: "CestaId");

            migrationBuilder.CreateIndex(
                name: "IX_ordens_compra_ContaGraficaId",
                table: "ordens_compra",
                column: "ContaGraficaId");

            migrationBuilder.CreateIndex(
                name: "IX_vendas_rebalanceamento_ClienteId_DataVenda",
                table: "vendas_rebalanceamento",
                columns: new[] { "ClienteId", "DataVenda" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custodias");

            migrationBuilder.DropTable(
                name: "historico_valor_mensal");

            migrationBuilder.DropTable(
                name: "itens_cesta");

            migrationBuilder.DropTable(
                name: "itens_distribuicao");

            migrationBuilder.DropTable(
                name: "itens_ordem_compra");

            migrationBuilder.DropTable(
                name: "vendas_rebalanceamento");

            migrationBuilder.DropTable(
                name: "distribuicoes");

            migrationBuilder.DropTable(
                name: "ordens_compra");

            migrationBuilder.DropTable(
                name: "cestas");

            migrationBuilder.DropTable(
                name: "contas_graficas");

            migrationBuilder.DropTable(
                name: "clientes");
        }
    }
}
