using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Health.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateInBodyReadingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbody_reading",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HeightCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    FatPct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    MusclePct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    BoneDensity = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    BodyDensity = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_inbody_reading", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inbody_reading_SwimmerId_ReadingDate",
                schema: "health",
                table: "inbody_reading",
                columns: new[] { "SwimmerId", "ReadingDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbody_reading",
                schema: "health");
        }
    }
}
