using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OdontoKlinika.Migrations
{
    /// <inheritdoc />
    public partial class isimtys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DarboIsimtys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GydytojoId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArDirba = table.Column<bool>(type: "bit", nullable: false),
                    Pradzia = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Pabaiga = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Priezastis = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DarboIsimtys", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DarboIsimtys");
        }
    }
}
