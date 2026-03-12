using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WpfResourceGantt.Migrations
{
    /// <inheritdoc />
    public partial class AddRowVersionConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WorkItems",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Systems",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ResourceAssignments",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProgressItems",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProgressHistory",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProgressBlocks",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "AdminTasks",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "RowVersion", table: "WorkItems");
            migrationBuilder.DropColumn(name: "RowVersion", table: "Users");
            migrationBuilder.DropColumn(name: "RowVersion", table: "Systems");
            migrationBuilder.DropColumn(name: "RowVersion", table: "ResourceAssignments");
            migrationBuilder.DropColumn(name: "RowVersion", table: "ProgressItems");
            migrationBuilder.DropColumn(name: "RowVersion", table: "ProgressHistory");
            migrationBuilder.DropColumn(name: "RowVersion", table: "ProgressBlocks");
            migrationBuilder.DropColumn(name: "RowVersion", table: "AdminTasks");
        }
    }
}
