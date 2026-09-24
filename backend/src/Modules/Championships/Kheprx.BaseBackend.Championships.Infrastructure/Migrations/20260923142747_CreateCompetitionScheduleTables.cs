using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Championships.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateCompetitionScheduleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "competition_day",
                schema: "championships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelEn = table.Column<string>(type: "text", nullable: false),
                    LabelAr = table.Column<string>(type: "text", nullable: true),
                    DayDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competition_day", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "race_assignment",
                schema: "championships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RaceSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_race_assignment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "race_session",
                schema: "championships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DayId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrokeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DistanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_race_session", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_competition_day_EventId",
                schema: "championships",
                table: "competition_day",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_race_assignment_RaceSessionId_SwimmerId",
                schema: "championships",
                table: "race_assignment",
                columns: new[] { "RaceSessionId", "SwimmerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_race_session_DayId",
                schema: "championships",
                table: "race_session",
                column: "DayId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "competition_day",
                schema: "championships");

            migrationBuilder.DropTable(
                name: "race_assignment",
                schema: "championships");

            migrationBuilder.DropTable(
                name: "race_session",
                schema: "championships");
        }
    }
}
