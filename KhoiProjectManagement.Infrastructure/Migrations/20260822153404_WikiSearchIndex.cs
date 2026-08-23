using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WikiSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrentContentMarkdown",
                table: "WikiPages",
                type: "text",
                nullable: true);

            // Backfill existing pages from their latest version - without this, every page created
            // before this migration would be invisible to search until someone happened to re-save it.
            migrationBuilder.Sql(@"
                UPDATE ""WikiPages"" p
                SET ""CurrentContentMarkdown"" = latest.""ContentMarkdown""
                FROM (
                    SELECT DISTINCT ON (""WikiPageId"") ""WikiPageId"", ""ContentMarkdown""
                    FROM ""WikiPageVersions""
                    ORDER BY ""WikiPageId"", ""VersionNumber"" DESC
                ) AS latest
                WHERE p.""Id"" = latest.""WikiPageId"";
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentContentMarkdown",
                table: "WikiPages");
        }
    }
}
