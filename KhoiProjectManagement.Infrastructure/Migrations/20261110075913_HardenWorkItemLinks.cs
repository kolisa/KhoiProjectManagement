using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenWorkItemLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemLinks_SourceId_TargetId",
                table: "WorkItemLinks");

            migrationBuilder.AddColumn<string>(
                name: "PairKey",
                table: "WorkItemLinks",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            // Backfill the direction-independent key for existing rows, then drop any duplicate pair (e.g. A->B
            // and B->A, which the old (SourceId, TargetId) index allowed) keeping the oldest, so the unique
            // index below can be created.
            migrationBuilder.Sql(
                "UPDATE \"WorkItemLinks\" SET \"PairKey\" = LEAST(\"SourceId\", \"TargetId\")::text || '-' || GREATEST(\"SourceId\", \"TargetId\")::text;");
            migrationBuilder.Sql(
                "DELETE FROM \"WorkItemLinks\" a USING \"WorkItemLinks\" b WHERE a.\"PairKey\" = b.\"PairKey\" AND a.\"Id\" > b.\"Id\";");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_PairKey",
                table: "WorkItemLinks",
                column: "PairKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_SourceId",
                table: "WorkItemLinks",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemLinks_PairKey",
                table: "WorkItemLinks");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemLinks_SourceId",
                table: "WorkItemLinks");

            migrationBuilder.DropColumn(
                name: "PairKey",
                table: "WorkItemLinks");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_SourceId_TargetId",
                table: "WorkItemLinks",
                columns: new[] { "SourceId", "TargetId" },
                unique: true);
        }
    }
}
