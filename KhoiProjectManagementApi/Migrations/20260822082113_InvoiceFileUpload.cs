using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagementApi.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceFileUpload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FileContentType",
                table: "Invoices",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "Invoices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "Invoices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoredFileName",
                table: "Invoices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileContentType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "StoredFileName",
                table: "Invoices");
        }
    }
}
