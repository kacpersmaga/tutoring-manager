using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoringManager.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLessonsAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: true),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    local_start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attendance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attendance_responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    price_per_lesson = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    is_modified_from_series = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lessons", x => x.id);
                    table.ForeignKey(
                        name: "fk_lessons_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lesson_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_lesson_notes_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lesson_notes_lesson_id",
                table: "lesson_notes",
                column: "lesson_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lessons_student_id",
                table: "lessons",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_lessons_tutor_id_local_date",
                table: "lessons",
                columns: new[] { "tutor_id", "local_date" });

            migrationBuilder.CreateIndex(
                name: "ix_lessons_tutor_id_start_utc",
                table: "lessons",
                columns: new[] { "tutor_id", "start_utc" });

            // No two non-cancelled lessons of one tutor may overlap in time. Enforced by the database,
            // so two concurrent requests cannot both succeed.
            migrationBuilder.Sql("""
                ALTER TABLE lessons ADD CONSTRAINT lessons_no_overlap
                EXCLUDE USING gist (tutor_id WITH =, tstzrange(start_utc, end_utc) WITH &&)
                WHERE (status <> 'Cancelled');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lesson_notes");

            migrationBuilder.DropTable(
                name: "lessons");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:btree_gist", ",,");
        }
    }
}
