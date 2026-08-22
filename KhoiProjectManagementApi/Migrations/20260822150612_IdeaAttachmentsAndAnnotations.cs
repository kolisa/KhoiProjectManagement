using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KhoiProjectManagementApi.Migrations
{
    /// <inheritdoc />
    public partial class IdeaAttachmentsAndAnnotations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdeaAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdeaId = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<int>(type: "integer", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeaAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeaAttachments_Ideas_IdeaId",
                        column: x => x.IdeaId,
                        principalTable: "Ideas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IdeaAttachments_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdeaAttachmentAnnotations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    IdeaAttachmentId = table.Column<int>(type: "integer", nullable: false),
                    AuthoredBy = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdeaAttachmentAnnotations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdeaAttachmentAnnotations_IdeaAttachments_IdeaAttachmentId",
                        column: x => x.IdeaAttachmentId,
                        principalTable: "IdeaAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IdeaAttachmentAnnotations_Users_AuthoredBy",
                        column: x => x.AuthoredBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdeaAttachmentAnnotations_AuthoredBy",
                table: "IdeaAttachmentAnnotations",
                column: "AuthoredBy");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaAttachmentAnnotations_IdeaAttachmentId",
                table: "IdeaAttachmentAnnotations",
                column: "IdeaAttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaAttachments_IdeaId",
                table: "IdeaAttachments",
                column: "IdeaId");

            migrationBuilder.CreateIndex(
                name: "IX_IdeaAttachments_UploadedBy",
                table: "IdeaAttachments",
                column: "UploadedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdeaAttachmentAnnotations");

            migrationBuilder.DropTable(
                name: "IdeaAttachments");
        }
    }
}
