using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemEffortAndRichDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Tasks",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.CreateTable(
                name: "WorkItemEfforts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Points = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemEfforts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemEfforts_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemEfforts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemEfforts_TaskId_UserId_Category",
                table: "WorkItemEfforts",
                columns: new[] { "TaskId", "UserId", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemEfforts_UserId",
                table: "WorkItemEfforts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemEfforts");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Tasks",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20000)",
                oldMaxLength: 20000);
        }
    }
}
