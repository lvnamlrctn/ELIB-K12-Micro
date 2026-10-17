using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoanPolicyHoldDaysFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Migration Holds thêm cột hold_days với mặc định 0 — chính sách có từ trước mang 0 ngày giữ sách, mọi lần lưu (và gán loại bạn
            // đọc cho điểm, chép từ chính sách đó) đều bị chặn "Số ngày giữ sách từ 1 đến 30". Đưa về mặc định LoanPolicy.DefaultHoldDays.
            // Bảng có FORCE RLS, role service không BYPASSRLS → bật ngữ cảnh hệ thống trong transaction của migration (set_config cục bộ),
            // không thì UPDATE chỉ thấy 0 dòng.
            migrationBuilder.Sql("""
                SELECT set_config('app.tenant_system', 'on', true);
                UPDATE loan_policies SET hold_days = 2 WHERE hold_days < 1;
                UPDATE loan_policies SET hold_days = 30 WHERE hold_days > 30;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
