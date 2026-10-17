using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Patron.Infrastructure.Migrations
{
    /// <summary>Vá sót: migration ReaderExtraPhotos tạo reader_photos nhưng quên bật RLS (phát hiện qua smoke.sh khi triển khai).</summary>
    public partial class ReaderPhotosTenantRls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnableTenantRls("reader_photos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("reader_photos");
        }
    }
}
