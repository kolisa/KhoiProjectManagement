using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExplicitDueDateFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasExplicitDueDate",
                table: "Tasks",
                type: "boolean",
                nullable: false,
                defaultValue: true); // every existing task keeps its due date as a real deadline
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasExplicitDueDate",
                table: "Tasks");
        }
    }
}
