using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplateGates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ProjectTemplateId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateGates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateGates_ProjectTemplates_ProjectTemplateId",
                        column: x => x.ProjectTemplateId,
                        principalTable: "ProjectTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateProgressBlocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TemplateGateId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateProgressBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateProgressBlocks_TemplateGates_TemplateGateId",
                        column: x => x.TemplateGateId,
                        principalTable: "TemplateGates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateProgressItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TemplateProgressBlockId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateProgressItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateProgressItems_TemplateProgressBlocks_TemplateProgressBlockId",
                        column: x => x.TemplateProgressBlockId,
                        principalTable: "TemplateProgressBlocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TemplateGates_ProjectTemplateId",
                table: "TemplateGates",
                column: "ProjectTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateProgressBlocks_TemplateGateId",
                table: "TemplateProgressBlocks",
                column: "TemplateGateId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateProgressItems_TemplateProgressBlockId",
                table: "TemplateProgressItems",
                column: "TemplateProgressBlockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateProgressItems");

            migrationBuilder.DropTable(
                name: "TemplateProgressBlocks");

            migrationBuilder.DropTable(
                name: "TemplateGates");

            migrationBuilder.DropTable(
                name: "ProjectTemplates");
        }
    }
}
