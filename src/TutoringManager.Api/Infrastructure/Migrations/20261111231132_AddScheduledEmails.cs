using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoringManager.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "scheduled_emails",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    to_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    send_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    locked_until_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduled_emails", x => x.id);
                    table.ForeignKey(
                        name: "fk_scheduled_emails_lessons_lesson_id",
                        column: x => x.lesson_id,
                        principalTable: "lessons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_emails_lesson_id",
                table: "scheduled_emails",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_emails_status_next_attempt_at_utc",
                table: "scheduled_emails",
                columns: new[] { "status", "next_attempt_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "scheduled_emails");
        }
    }
}
