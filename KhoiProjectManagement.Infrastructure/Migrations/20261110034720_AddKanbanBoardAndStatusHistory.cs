using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKanbanBoardAndStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BoardColumns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    WipLimit = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardColumns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoardColumns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskStatusHistories_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskStatusHistories_Users_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoardColumns_ProjectId_Status",
                table: "BoardColumns",
                columns: new[] { "ProjectId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusHistories_ChangedAt",
                table: "TaskStatusHistories",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusHistories_ChangedById",
                table: "TaskStatusHistories",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_TaskStatusHistories_TaskId_ChangedAt",
                table: "TaskStatusHistories",
                columns: new[] { "TaskId", "ChangedAt" });

            // Baseline history for work that predates status tracking, so cumulative-flow charts have a
            // starting point: everything began as "todo" at creation, and anything no longer "todo" moved
            // to its current status at its last update (CompletedAt for finished items). Approximate by
            // necessity - the real intermediate transitions were never recorded.
            migrationBuilder.Sql(@"
                INSERT INTO ""TaskStatusHistories"" (""TaskId"", ""FromStatus"", ""ToStatus"", ""ChangedAt"")
                SELECT ""Id"", NULL, 'todo', ""CreatedAt"" FROM ""Tasks"";
                INSERT INTO ""TaskStatusHistories"" (""TaskId"", ""FromStatus"", ""ToStatus"", ""ChangedAt"")
                SELECT ""Id"", 'todo', ""Status"", COALESCE(""CompletedAt"", ""UpdatedAt"") FROM ""Tasks"" WHERE ""Status"" <> 'todo';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardColumns");

            migrationBuilder.DropTable(
                name: "TaskStatusHistories");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Tasks");
        }
    }
}
