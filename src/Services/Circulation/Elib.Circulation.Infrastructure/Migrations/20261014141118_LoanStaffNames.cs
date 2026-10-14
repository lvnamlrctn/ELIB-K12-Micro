using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoanStaffNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "loaned_by_name",
                table: "loans",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "returned_by_name",
                table: "loans",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "returned_late",
                table: "loans",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Lượt đã trả trước bản này: trả sau ngày hết hạn (ngày lịch Việt Nam) = trả quá hạn, như Loan.OverdueDays.
            migrationBuilder.Sql("""
                UPDATE loans SET returned_late = true
                WHERE returned_at IS NOT NULL
                  AND (returned_at AT TIME ZONE 'UTC' + interval '7 hours')::date > (due_at AT TIME ZONE 'UTC' + interval '7 hours')::date;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id_store_id",
                table: "loans",
                columns: new[] { "tenant_id", "store_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_loans_tenant_id_store_id",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "loaned_by_name",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "returned_by_name",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "returned_late",
                table: "loans");
        }
    }
}
