using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuardianAndGuardianRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guardian_relation",
                schema: "reference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardian_relation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guardian",
                schema: "athlete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NationalId = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardian", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guardian_guardian_relation_RelationId",
                        column: x => x.RelationId,
                        principalSchema: "reference",
                        principalTable: "guardian_relation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guardian_swimmer_profile_SwimmerId",
                        column: x => x.SwimmerId,
                        principalSchema: "identity",
                        principalTable: "swimmer_profile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guardian_RelationId",
                schema: "athlete",
                table: "guardian",
                column: "RelationId");

            migrationBuilder.CreateIndex(
                name: "IX_guardian_SwimmerId_RelationId",
                schema: "athlete",
                table: "guardian",
                columns: new[] { "SwimmerId", "RelationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guardian_relation_Code",
                schema: "reference",
                table: "guardian_relation",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guardian",
                schema: "athlete");

            migrationBuilder.DropTable(
                name: "guardian_relation",
                schema: "reference");
        }
    }
}
