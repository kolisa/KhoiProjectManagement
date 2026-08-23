using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WikiAnchoredComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnchorBlockIndex",
                table: "WikiPageComments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnchorText",
                table: "WikiPageComments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnchorBlockIndex",
                table: "WikiPageComments");

            migrationBuilder.DropColumn(
                name: "AnchorText",
                table: "WikiPageComments");
        }
    }
}
