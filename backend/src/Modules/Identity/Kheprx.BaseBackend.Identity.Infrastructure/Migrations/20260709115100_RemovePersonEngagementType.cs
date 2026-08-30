using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemovePersonEngagementType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TypeId",
                schema: "identity",
                table: "workers");

            migrationBuilder.DropColumn(
                name: "TypeId",
                schema: "identity",
                table: "moqaweleen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TypeId",
                schema: "identity",
                table: "workers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TypeId",
                schema: "identity",
                table: "moqaweleen",
                type: "uuid",
                nullable: true);
        }
    }
}
