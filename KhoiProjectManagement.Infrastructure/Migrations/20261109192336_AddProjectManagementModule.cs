using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KhoiProjectManagement.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectManagementModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks");

            migrationBuilder.AddColumn<bool>(
                name: "PmAccess",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PmOnly",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Rank",
                table: "Tasks",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "StoryPoints",
                table: "Tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectRole",
                table: "ProjectUsers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Developer");

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Action", "Description", "Name", "Resource" },
                values: new object[] { 29, "admin", "Grant or revoke Project Management module access (and module-only confinement) for users", "pm.admin", "pm" });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "PmAccess", "PmOnly" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "PmAccess", "PmOnly" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "PmAccess", "PmOnly" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "PmAccess", "PmOnly" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "PmAccess", "PmOnly" },
                values: new object[] { false, false });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "PmAccess", "PmOnly" },
                values: new object[] { false, false });

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId" },
                values: new object[] { 29, 1 });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ParentId",
                table: "Tasks",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Rank",
                table: "Tasks",
                columns: new[] { "ProjectId", "Rank" });

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Tasks_ParentId",
                table: "Tasks",
                column: "ParentId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Tasks_ParentId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ParentId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Rank",
                table: "Tasks");

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { 29, 1 });

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DropColumn(
                name: "PmAccess",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PmOnly",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Rank",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "StoryPoints",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ProjectRole",
                table: "ProjectUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");
        }
    }
}
