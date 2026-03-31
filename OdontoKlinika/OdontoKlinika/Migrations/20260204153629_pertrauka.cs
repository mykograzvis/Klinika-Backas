using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OdontoKlinika.Migrations
{
    /// <inheritdoc />
    public partial class pertrauka : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PertraukaIki",
                table: "DarboGrafikai",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PertraukaNuo",
                table: "DarboGrafikai",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PertraukaIki",
                table: "DarboGrafikai");

            migrationBuilder.DropColumn(
                name: "PertraukaNuo",
                table: "DarboGrafikai");
        }
    }
}
