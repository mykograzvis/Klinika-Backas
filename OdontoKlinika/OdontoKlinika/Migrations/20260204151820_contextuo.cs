using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OdontoKlinika.Migrations
{
    /// <inheritdoc />
    public partial class contextuo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DarboGrafikai",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GydytojoId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SavaitesDiena = table.Column<int>(type: "int", nullable: false),
                    Dirba = table.Column<bool>(type: "bit", nullable: false),
                    Pradzia = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pabaiga = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DarboGrafikai", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vartotojai",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Vardas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pavarde = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ElPastas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SlaptazodisHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Telefonas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amzius = table.Column<int>(type: "int", nullable: false),
                    AsmensKodas = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    KraujoGrupe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TwoFactorSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsTwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    VartotojoTipas = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: false),
                    Specializacija = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DarboPatirtisMetais = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vartotojai", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vizitai",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PacientasId = table.Column<int>(type: "int", nullable: false),
                    GydytojasId = table.Column<int>(type: "int", nullable: false),
                    PradziosLaikas = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PabaigosLaikas = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Busena = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Pastabos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SukurimoData = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Apmoketa = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vizitai", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vizitai_Vartotojai_GydytojasId",
                        column: x => x.GydytojasId,
                        principalTable: "Vartotojai",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vizitai_Vartotojai_PacientasId",
                        column: x => x.PacientasId,
                        principalTable: "Vartotojai",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Proceduros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Pavadinimas = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Aprasymas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kaina = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VizitasId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proceduros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proceduros_Vizitai_VizitasId",
                        column: x => x.VizitasId,
                        principalTable: "Vizitai",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proceduros_VizitasId",
                table: "Proceduros",
                column: "VizitasId");

            migrationBuilder.CreateIndex(
                name: "IX_Vartotojai_AsmensKodas",
                table: "Vartotojai",
                column: "AsmensKodas",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vizitai_GydytojasId",
                table: "Vizitai",
                column: "GydytojasId");

            migrationBuilder.CreateIndex(
                name: "IX_Vizitai_PacientasId",
                table: "Vizitai",
                column: "PacientasId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DarboGrafikai");

            migrationBuilder.DropTable(
                name: "Proceduros");

            migrationBuilder.DropTable(
                name: "Vizitai");

            migrationBuilder.DropTable(
                name: "Vartotojai");
        }
    }
}
