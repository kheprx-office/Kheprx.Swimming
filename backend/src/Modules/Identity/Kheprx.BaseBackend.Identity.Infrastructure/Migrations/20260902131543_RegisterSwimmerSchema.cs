using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RegisterSwimmerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM identity.swimmer_profile;");

            migrationBuilder.DropColumn(
                name: "NameAr",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropColumn(
                name: "NameEn",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.EnsureSchema(
                name: "athlete");

            migrationBuilder.AddColumn<Guid>(
                name: "BloodTypeId",
                schema: "identity",
                table: "swimmer_profile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RepresentChampionshipClubId",
                schema: "identity",
                table: "swimmer_profile",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TrainingClubId",
                schema: "identity",
                table: "swimmer_profile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "identity",
                table: "swimmer_profile",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "identity",
                table: "swimmer_profile",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "swimmer_specialization",
                schema: "athlete",
                columns: table => new
                {
                    SwimmerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrokeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_swimmer_specialization", x => new { x.SwimmerProfileId, x.StrokeId });
                    table.ForeignKey(
                        name: "FK_swimmer_specialization_stroke_StrokeId",
                        column: x => x.StrokeId,
                        principalSchema: "reference",
                        principalTable: "stroke",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_swimmer_specialization_swimmer_profile_SwimmerProfileId",
                        column: x => x.SwimmerProfileId,
                        principalSchema: "identity",
                        principalTable: "swimmer_profile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_swimmer_profile_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile",
                column: "BloodTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_swimmer_profile_RepresentChampionshipClubId",
                schema: "identity",
                table: "swimmer_profile",
                column: "RepresentChampionshipClubId");

            migrationBuilder.CreateIndex(
                name: "IX_swimmer_profile_TrainingClubId",
                schema: "identity",
                table: "swimmer_profile",
                column: "TrainingClubId");

            migrationBuilder.CreateIndex(
                name: "IX_swimmer_profile_UserId",
                schema: "identity",
                table: "swimmer_profile",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_swimmer_specialization_StrokeId",
                schema: "athlete",
                table: "swimmer_specialization",
                column: "StrokeId");

            migrationBuilder.AddForeignKey(
                name: "FK_swimmer_profile_app_user_UserId",
                schema: "identity",
                table: "swimmer_profile",
                column: "UserId",
                principalSchema: "identity",
                principalTable: "app_user",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_swimmer_profile_blood_type_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile",
                column: "BloodTypeId",
                principalSchema: "reference",
                principalTable: "blood_type",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_swimmer_profile_club_RepresentChampionshipClubId",
                schema: "identity",
                table: "swimmer_profile",
                column: "RepresentChampionshipClubId",
                principalSchema: "reference",
                principalTable: "club",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_swimmer_profile_club_TrainingClubId",
                schema: "identity",
                table: "swimmer_profile",
                column: "TrainingClubId",
                principalSchema: "reference",
                principalTable: "club",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_swimmer_profile_app_user_UserId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropForeignKey(
                name: "FK_swimmer_profile_blood_type_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropForeignKey(
                name: "FK_swimmer_profile_club_RepresentChampionshipClubId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropForeignKey(
                name: "FK_swimmer_profile_club_TrainingClubId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropTable(
                name: "swimmer_specialization",
                schema: "athlete");

            migrationBuilder.DropIndex(
                name: "IX_swimmer_profile_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropIndex(
                name: "IX_swimmer_profile_RepresentChampionshipClubId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropIndex(
                name: "IX_swimmer_profile_TrainingClubId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropIndex(
                name: "IX_swimmer_profile_UserId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropColumn(
                name: "BloodTypeId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropColumn(
                name: "RepresentChampionshipClubId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropColumn(
                name: "TrainingClubId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                schema: "identity",
                table: "swimmer_profile",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                schema: "identity",
                table: "swimmer_profile",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
