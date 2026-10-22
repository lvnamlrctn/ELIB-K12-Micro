using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoanCloseKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "close_kind",
                table: "loans",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "close_reason",
                table: "loans",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reopen_count",
                table: "loans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Lượt đã đóng từ trước chưa ghi cách đóng: suy ra từ trạng thái bản sách khi đóng. Bảng có FORCE RLS → ngữ cảnh hệ thống trong transaction migration.
            migrationBuilder.Sql("""
                SELECT set_config('app.tenant_system', 'on', true);
                UPDATE loans SET close_kind = CASE WHEN closed_item_status = 'L' THEN 'Lost' ELSE 'Returned' END
                WHERE returned_at IS NOT NULL AND close_kind IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "close_kind",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "close_reason",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "reopen_count",
                table: "loans");
        }
    }
}
