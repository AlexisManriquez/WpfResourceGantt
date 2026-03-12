using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSystemPMAddManagedProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectManagerId",
                table: "Systems");

            migrationBuilder.AddColumn<string>(
                name: "ManagedProjectIds",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManagedProjectIds",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "ProjectManagerId",
                table: "Systems",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
