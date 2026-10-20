using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <summary>
    /// Áp RLS còn thiếu cho 4 bảng mới (map_fixtures, map_fixture_equipment, map_floor_utilities, store_replicas) — sót ở các
    /// migration trước vì EnableTenantRls không tự sinh qua `dotnet ef migrations add` (chỉ dò theo model, không biết quy ước
    /// RLS), phải gọi tay. 3 migration trước đã được sửa lại cho lần cài mới; migration này chỉ bù cho DB đã chạy các
    /// migration đó trước khi phát hiện thiếu.
    /// </summary>
    public partial class EnableRlsForFixtureTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnableTenantRls("map_fixtures");
            migrationBuilder.EnableTenantRls("map_fixture_equipment");
            migrationBuilder.EnableTenantRls("map_floor_utilities");
            migrationBuilder.EnableTenantRls("store_replicas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("map_fixtures");
            migrationBuilder.DisableTenantRls("map_fixture_equipment");
            migrationBuilder.DisableTenantRls("map_floor_utilities");
            migrationBuilder.DisableTenantRls("store_replicas");
        }
    }
}
