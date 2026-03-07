using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TemplateTaskId",
                table: "TemplateProgressBlocks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TemplateTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    WorkHours = table.Column<double>(type: "float", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    Predecessors = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TemplateGateId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateTasks_TemplateGates_TemplateGateId",
                        column: x => x.TemplateGateId,
                        principalTable: "TemplateGates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateProgressBlocks_TemplateTaskId",
                table: "TemplateProgressBlocks",
                column: "TemplateTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateTasks_TemplateGateId",
                table: "TemplateTasks",
                column: "TemplateGateId");

            migrationBuilder.AddForeignKey(
                name: "FK_TemplateProgressBlocks_TemplateTasks_TemplateTaskId",
                table: "TemplateProgressBlocks",
                column: "TemplateTaskId",
                principalTable: "TemplateTasks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TemplateProgressBlocks_TemplateTasks_TemplateTaskId",
                table: "TemplateProgressBlocks");

            migrationBuilder.DropTable(
                name: "TemplateTasks");

            migrationBuilder.DropIndex(
                name: "IX_TemplateProgressBlocks_TemplateTaskId",
                table: "TemplateProgressBlocks");

            migrationBuilder.DropColumn(
                name: "TemplateTaskId",
                table: "TemplateProgressBlocks");
        }
    }
}
