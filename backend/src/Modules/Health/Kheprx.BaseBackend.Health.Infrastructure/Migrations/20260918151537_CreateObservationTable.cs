using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Health.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateObservationTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "observation",
                schema: "health",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ObservedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observation",
                schema: "health");
        }
    }
}
