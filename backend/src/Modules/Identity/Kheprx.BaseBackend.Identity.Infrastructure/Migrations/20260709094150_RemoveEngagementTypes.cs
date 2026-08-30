using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEngagementTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_moqaweleen_engagement_types_TypeId",
                schema: "identity",
                table: "moqaweleen");

            migrationBuilder.DropForeignKey(
                name: "FK_workers_engagement_types_TypeId",
                schema: "identity",
                table: "workers");

            migrationBuilder.DropTable(
                name: "engagement_types",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_workers_TypeId",
                schema: "identity",
                table: "workers");

            migrationBuilder.DropIndex(
                name: "IX_moqaweleen_TypeId",
                schema: "identity",
                table: "moqaweleen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "engagement_types",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LabelAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LabelEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagement_types", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workers_TypeId",
                schema: "identity",
                table: "workers",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_moqaweleen_TypeId",
                schema: "identity",
                table: "moqaweleen",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_engagement_types_Code",
                schema: "identity",
                table: "engagement_types",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_moqaweleen_engagement_types_TypeId",
                schema: "identity",
                table: "moqaweleen",
                column: "TypeId",
                principalSchema: "identity",
                principalTable: "engagement_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_workers_engagement_types_TypeId",
                schema: "identity",
                table: "workers",
                column: "TypeId",
                principalSchema: "identity",
                principalTable: "engagement_types",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
