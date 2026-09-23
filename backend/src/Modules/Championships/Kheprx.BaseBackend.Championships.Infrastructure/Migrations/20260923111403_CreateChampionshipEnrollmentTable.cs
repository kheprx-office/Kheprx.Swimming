using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Championships.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateChampionshipEnrollmentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "championship_enrollment",
                schema: "championships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_championship_enrollment", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_championship_enrollment_EventId_SwimmerId",
                schema: "championships",
                table: "championship_enrollment",
                columns: new[] { "EventId", "SwimmerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "championship_enrollment",
                schema: "championships");
        }
    }
}
