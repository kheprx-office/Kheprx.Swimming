using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Attendance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateAttendanceRecordTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "attendance");

            migrationBuilder.CreateTable(
                name: "attendance_record",
                schema: "attendance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StatusId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CoachNoteEn = table.Column<string>(type: "text", nullable: true),
                    CoachNoteAr = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_record", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_record_SwimmerId_SessionDate",
                schema: "attendance",
                table: "attendance_record",
                columns: new[] { "SwimmerId", "SessionDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attendance_record",
                schema: "attendance");
        }
    }
}
