using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropSwimmerBloodType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_swimmer_profile_blood_type_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropIndex(
                name: "IX_swimmer_profile_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile");

            migrationBuilder.DropColumn(
                name: "BloodTypeId",
                schema: "identity",
                table: "swimmer_profile");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BloodTypeId",
                schema: "identity",
                table: "swimmer_profile",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_swimmer_profile_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile",
                column: "BloodTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_swimmer_profile_blood_type_BloodTypeId",
                schema: "identity",
                table: "swimmer_profile",
                column: "BloodTypeId",
                principalSchema: "reference",
                principalTable: "blood_type",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
