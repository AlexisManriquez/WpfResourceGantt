using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_VisualizationUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineEndDate",
                table: "WorkItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BaselineStartDate",
                table: "WorkItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOverAllocated",
                table: "WorkItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ItemType",
                table: "WorkItems",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "WeeklyCapacity",
                table: "Users",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaselineEndDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "BaselineStartDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "IsOverAllocated",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "WeeklyCapacity",
                table: "Users");
        }
    }
}
