using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ScheduleMode",
                table: "WorkItems",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduleMode",
                table: "WorkItems");
        }
    }
}
