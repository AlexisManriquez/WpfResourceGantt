using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class AddEvmToSystemItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Acwp",
                table: "Systems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Bcwp",
                table: "Systems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Bcws",
                table: "Systems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Progress",
                table: "Systems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Acwp",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Bcwp",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Bcws",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "Systems");
        }
    }
}
