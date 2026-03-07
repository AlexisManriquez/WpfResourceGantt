using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class AddEvmCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Phase 2: SMTS Import Audit Trail ─────────────────────────────
            // Adds two nullable columns to WorkItems to track when and from
            // which file ACWP was last updated via SMTS CSV import.
            migrationBuilder.AddColumn<DateTime>(
                name: "LastAcwpImportDate",
                table: "WorkItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAcwpImportSource",
                table: "WorkItems",
                type: "nvarchar(max)",
                nullable: true);

            // ── Phase 3: Weekly EVM Snapshots ────────────────────────────────
            // New table to store frozen per-week EVM metrics at SubProject level.
            // Powers the S-Curve and provides reproducible data for customer reports.
            migrationBuilder.CreateTable(
                name: "EvmWeeklySnapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubProjectId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    WeekEndingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BAC = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BCWS = table.Column<double>(type: "float", nullable: false),
                    BCWP = table.Column<double>(type: "float", nullable: false),
                    ACWP = table.Column<double>(type: "float", nullable: false),
                    SPI = table.Column<double>(type: "float", nullable: false),
                    CPI = table.Column<double>(type: "float", nullable: false),
                    Progress = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvmWeeklySnapshots", x => x.Id);
                });

            // Unique constraint: one snapshot per SubProject per week.
            migrationBuilder.CreateIndex(
                name: "IX_EvmWeeklySnapshots_SubProjectId_WeekEndingDate",
                table: "EvmWeeklySnapshots",
                columns: new[] { "SubProjectId", "WeekEndingDate" },
                unique: true,
                filter: "[SubProjectId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EvmWeeklySnapshots");

            migrationBuilder.DropColumn(name: "LastAcwpImportDate", table: "WorkItems");
            migrationBuilder.DropColumn(name: "LastAcwpImportSource", table: "WorkItems");
        }
    }
}
