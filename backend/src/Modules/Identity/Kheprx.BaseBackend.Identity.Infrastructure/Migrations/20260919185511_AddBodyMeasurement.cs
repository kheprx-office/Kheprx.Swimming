using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBodyMeasurement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "body_measurement",
                schema: "athlete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MeasuredAt = table.Column<DateOnly>(type: "date", nullable: false),
                    RightArmCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    LeftArmCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    RightLegCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    LeftLegCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    TorsoCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    BustDiameterCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    WaistDiameterCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_body_measurement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_body_measurement_swimmer_profile_SwimmerId",
                        column: x => x.SwimmerId,
                        principalSchema: "identity",
                        principalTable: "swimmer_profile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_body_measurement_SwimmerId_MeasuredAt",
                schema: "athlete",
                table: "body_measurement",
                columns: new[] { "SwimmerId", "MeasuredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "body_measurement",
                schema: "athlete");
        }
    }
}
