using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Health.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateHealthReadingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_reading",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalTestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(8,2)", nullable: false),
                    ReadingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_reading", x => x.Id);
                    table.ForeignKey(
                        name: "FK_health_reading_medical_test_MedicalTestId",
                        column: x => x.MedicalTestId,
                        principalSchema: "health",
                        principalTable: "medical_test",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_health_reading_MedicalTestId",
                schema: "health",
                table: "health_reading",
                column: "MedicalTestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_reading",
                schema: "health");
        }
    }
}
