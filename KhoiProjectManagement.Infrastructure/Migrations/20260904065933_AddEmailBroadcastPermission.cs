using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailBroadcastPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Description", "Name", "Resource" },
                values: new object[] { 27, "broadcast", "Send a broadcast email to users filtered by role", "email.broadcast", "email" });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 27, 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 27, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 27);
        }
    }
}
