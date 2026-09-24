using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Championships.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateRaceResultTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "race_result",
                schema: "championships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeMs = table.Column<int>(type: "integer", nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    IsPersonalBest = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_race_result", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_race_result_RaceSessionId_SwimmerId",
                schema: "championships",
                table: "race_result",
                columns: new[] { "RaceSessionId", "SwimmerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "race_result",
                schema: "championships");
        }
    }
}
