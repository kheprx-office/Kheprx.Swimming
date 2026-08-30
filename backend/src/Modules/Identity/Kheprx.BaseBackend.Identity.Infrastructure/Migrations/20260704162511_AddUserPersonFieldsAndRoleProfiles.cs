using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPersonFieldsAndRoleProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Age",
                schema: "identity",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "identity",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                schema: "identity",
                table: "users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Nid",
                schema: "identity",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "engagement_types",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LabelAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LabelEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_engagement_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "managers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_managers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_managers_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "moqaweleen",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyWage = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moqaweleen", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moqaweleen_engagement_types_TypeId",
                        column: x => x.TypeId,
                        principalSchema: "identity",
                        principalTable: "engagement_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_moqaweleen_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyWage = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workers_engagement_types_TypeId",
                        column: x => x.TypeId,
                        principalSchema: "identity",
                        principalTable: "engagement_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workers_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_Code",
                schema: "identity",
                table: "users",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Nid",
                schema: "identity",
                table: "users",
                column: "Nid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_engagement_types_Code",
                schema: "identity",
                table: "engagement_types",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_managers_UserId",
                schema: "identity",
                table: "managers",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_moqaweleen_TypeId",
                schema: "identity",
                table: "moqaweleen",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_moqaweleen_UserId",
                schema: "identity",
                table: "moqaweleen",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workers_TypeId",
                schema: "identity",
                table: "workers",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_workers_UserId",
                schema: "identity",
                table: "workers",
                column: "UserId",
                unique: true);

            // ---- seed engagement types (fixed GUIDs; idempotent) ----
            migrationBuilder.Sql("""
                INSERT INTO identity.engagement_types ("Id", "Code", "LabelAr", "LabelEn", "SortOrder", "IsActive", "CreatedAt")
                VALUES
                  ('a1111111-1111-4111-8111-111111111111', 'daily',     'يومية',    'Daily',     1, true, now()),
                  ('a2222222-2222-4222-8222-222222222222', 'piecework', 'مقطوعية',  'Piecework', 2, true, now())
                ON CONFLICT ("Code") DO NOTHING;
                """);

            // ---- backfill codes per role prefix (ordered by CreatedAt for stable numbering) ----
            migrationBuilder.Sql("""
                WITH prefixed AS (
                  SELECT u."Id",
                         CASE r."Code"
                           WHEN 'admin'   THEN 'OWNER'
                           WHEN 'manager' THEN 'PM'
                           WHEN 'moqawel' THEN 'M'
                           WHEN 'worker'  THEN 'W'
                         END AS prefix,
                         ROW_NUMBER() OVER (PARTITION BY r."Code" ORDER BY u."CreatedAt", u."Id") AS rn
                  FROM identity.users u
                  JOIN identity.roles r ON r."Id" = u."RoleId"
                )
                UPDATE identity.users u
                SET "Code" = p.prefix || '-' || p.rn
                FROM prefixed p
                WHERE p."Id" = u."Id" AND u."Code" IS NULL AND p.prefix IS NOT NULL;
                """);

            // ---- backfill placeholder nids (corrected later via the edit form) ----
            migrationBuilder.Sql("""
                UPDATE identity.users
                SET "Nid" = 'PENDING-' || "Code"
                WHERE "Nid" IS NULL AND "Code" IS NOT NULL;
                """);

            // ---- default profile rows for existing role users ----
            migrationBuilder.Sql("""
                INSERT INTO identity.managers ("Id", "UserId", "MonthlySalary")
                SELECT gen_random_uuid(), u."Id", 0
                FROM identity.users u
                JOIN identity.roles r ON r."Id" = u."RoleId"
                WHERE r."Code" = 'manager'
                  AND NOT EXISTS (SELECT 1 FROM identity.managers m WHERE m."UserId" = u."Id");
                """);

            migrationBuilder.Sql("""
                INSERT INTO identity.moqaweleen ("Id", "UserId", "TypeId", "DailyWage")
                SELECT gen_random_uuid(), u."Id", 'a1111111-1111-4111-8111-111111111111', 0
                FROM identity.users u
                JOIN identity.roles r ON r."Id" = u."RoleId"
                WHERE r."Code" = 'moqawel'
                  AND NOT EXISTS (SELECT 1 FROM identity.moqaweleen m WHERE m."UserId" = u."Id");
                """);

            migrationBuilder.Sql("""
                INSERT INTO identity.workers ("Id", "UserId", "TypeId", "DailyWage", "HireDate")
                SELECT gen_random_uuid(), u."Id", 'a1111111-1111-4111-8111-111111111111', 0, NULL
                FROM identity.users u
                JOIN identity.roles r ON r."Id" = u."RoleId"
                WHERE r."Code" = 'worker'
                  AND NOT EXISTS (SELECT 1 FROM identity.workers w WHERE w."UserId" = u."Id");
                """);

            // ---- tighten: Nid becomes required now that every row has a value ----
            migrationBuilder.AlterColumn<string>(
                name: "Nid",
                schema: "identity",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "managers",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "moqaweleen",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "workers",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "engagement_types",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_users_Code",
                schema: "identity",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_Nid",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Age",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Gender",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Nid",
                schema: "identity",
                table: "users");
        }
    }
}
