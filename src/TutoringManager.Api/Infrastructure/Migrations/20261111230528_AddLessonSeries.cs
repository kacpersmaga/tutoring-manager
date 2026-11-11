using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoringManager.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "series_occurrence_date",
                table: "lessons",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lesson_series",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    generated_until = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_series", x => x.id);
                    table.ForeignKey(
                        name: "fk_lesson_series_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lessons_series_id",
                table: "lessons",
                column: "series_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_series_student_id",
                table: "lesson_series",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_lesson_series_tutor_id",
                table: "lesson_series",
                column: "tutor_id");

            migrationBuilder.AddForeignKey(
                name: "fk_lessons_lesson_series_series_id",
                table: "lessons",
                column: "series_id",
                principalTable: "lesson_series",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_lessons_lesson_series_series_id",
                table: "lessons");

            migrationBuilder.DropTable(
                name: "lesson_series");

            migrationBuilder.DropIndex(
                name: "ix_lessons_series_id",
                table: "lessons");

            migrationBuilder.DropColumn(
                name: "series_occurrence_date",
                table: "lessons");
        }
    }
}
