using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSystemExecutionMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualWork",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Acwp",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "BAC",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Bcwp",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Bcws",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "IsBaselined",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Systems");

            migrationBuilder.DropColumn(
                name: "Work",
                table: "Systems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ActualWork",
                table: "Systems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Acwp",
                table: "Systems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BAC",
                table: "Systems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
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

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                table: "Systems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsBaselined",
                table: "Systems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "Progress",
                table: "Systems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Systems",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "Work",
                table: "Systems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
