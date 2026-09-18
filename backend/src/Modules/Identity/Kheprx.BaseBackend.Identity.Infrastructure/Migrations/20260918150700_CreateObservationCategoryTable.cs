using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateObservationCategoryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "observation_category",
                schema: "reference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_observation_category", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_observation_category_Code",
                schema: "reference",
                table: "observation_category",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "observation_category",
                schema: "reference");
        }
    }
}
