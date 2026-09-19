using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kheprx.BaseBackend.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFitnessAssessmentAndMedicalExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fitness_assessment",
                schema: "reference",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fitness_assessment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "medical_exam",
                schema: "athlete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SwimmerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InternalMedId = table.Column<Guid>(type: "uuid", nullable: false),
                    HeartAssessId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpineAssessId = table.Column<Guid>(type: "uuid", nullable: false),
                    BloodTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Hemoglobin = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    HeightCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_exam", x => x.Id);
                    table.ForeignKey(
                        name: "FK_medical_exam_blood_type_BloodTypeId",
                        column: x => x.BloodTypeId,
                        principalSchema: "reference",
                        principalTable: "blood_type",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medical_exam_fitness_assessment_HeartAssessId",
                        column: x => x.HeartAssessId,
                        principalSchema: "reference",
                        principalTable: "fitness_assessment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medical_exam_fitness_assessment_InternalMedId",
                        column: x => x.InternalMedId,
                        principalSchema: "reference",
                        principalTable: "fitness_assessment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medical_exam_fitness_assessment_SpineAssessId",
                        column: x => x.SpineAssessId,
                        principalSchema: "reference",
                        principalTable: "fitness_assessment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medical_exam_swimmer_profile_SwimmerId",
                        column: x => x.SwimmerId,
                        principalSchema: "identity",
                        principalTable: "swimmer_profile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_fitness_assessment_Code",
                schema: "reference",
                table: "fitness_assessment",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medical_exam_BloodTypeId",
                schema: "athlete",
                table: "medical_exam",
                column: "BloodTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_medical_exam_HeartAssessId",
                schema: "athlete",
                table: "medical_exam",
                column: "HeartAssessId");

            migrationBuilder.CreateIndex(
                name: "IX_medical_exam_InternalMedId",
                schema: "athlete",
                table: "medical_exam",
                column: "InternalMedId");

            migrationBuilder.CreateIndex(
                name: "IX_medical_exam_SpineAssessId",
                schema: "athlete",
                table: "medical_exam",
                column: "SpineAssessId");

            migrationBuilder.CreateIndex(
                name: "IX_medical_exam_SwimmerId",
                schema: "athlete",
                table: "medical_exam",
                column: "SwimmerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_exam",
                schema: "athlete");

            migrationBuilder.DropTable(
                name: "fitness_assessment",
                schema: "reference");
        }
    }
}
