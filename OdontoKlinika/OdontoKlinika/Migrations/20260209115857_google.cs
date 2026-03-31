using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OdontoKlinika.Migrations
{
    /// <inheritdoc />
    public partial class google : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Vartotojai",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Vartotojai");
        }
    }
}
