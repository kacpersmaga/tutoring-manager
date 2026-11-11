using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TutoringManager.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    bank_format = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    imported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bank_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tutor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    counterparty_account = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    counterparty_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    transaction_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: true),
                    matched_by = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bank_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_bank_transactions_bank_imports_import_id",
                        column: x => x.import_id,
                        principalTable: "bank_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bank_transactions_students_student_id",
                        column: x => x.student_id,
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bank_imports_tutor_id_file_sha256",
                table: "bank_imports",
                columns: new[] { "tutor_id", "file_sha256" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bank_transactions_import_id",
                table: "bank_transactions",
                column: "import_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transactions_student_id",
                table: "bank_transactions",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_bank_transactions_tutor_id_student_id_booking_date",
                table: "bank_transactions",
                columns: new[] { "tutor_id", "student_id", "booking_date" });

            migrationBuilder.CreateIndex(
                name: "ix_bank_transactions_tutor_id_transaction_key",
                table: "bank_transactions",
                columns: new[] { "tutor_id", "transaction_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transactions");

            migrationBuilder.DropTable(
                name: "bank_imports");
        }
    }
}
