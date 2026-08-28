using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailLogStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "EmailLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill: every pre-existing row was already actually sent or failed long ago (Status=0/
            // Pending only means "not dispatched yet", which no historical row should say - otherwise
            // SendQueuedEmailsJob would try to re-send years-old emails on its first run after this
            // migration). EmailLogStatus: Pending=0, Sent=1, Failed=2.
            migrationBuilder.Sql(@"UPDATE ""EmailLogs"" SET ""Status"" = CASE WHEN ""IsSuccess"" THEN 1 ELSE 2 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "EmailLogs");
        }
    }
}
