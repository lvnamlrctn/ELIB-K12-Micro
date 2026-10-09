using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreCapacityAndDigitalStorageAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                schema: "PrintBook",
                table: "Store",
                type: "int",
                nullable: true);

            // Bảng có thể đã được tạo sẵn bằng script tay ở CSDL nào đó trước khi có migration này (theo
            // đúng mẫu Đợt 21 AdminUiPreference) — tạo có điều kiện để Database.Migrate() không lỗi.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'Ebook' AND t.name = 'DigitalStorageAuditResult')
BEGIN
    CREATE TABLE [Ebook].[DigitalStorageAuditResult] (
        [Id]               bigint IDENTITY(1,1) NOT NULL,
        [RunAt]            datetime2 NOT NULL,
        [TotalObjects]     bigint NOT NULL,
        [TotalSizeBytes]   bigint NOT NULL,
        [OrphanCount]      bigint NOT NULL,
        [DbFileCount]      bigint NOT NULL,
        [OrphanSampleJson] nvarchar(max) NULL,
        [BrokenFileCount]  bigint NULL,
        [BrokenSampleJson] nvarchar(max) NULL,
        CONSTRAINT [PK_DigitalStorageAuditResult] PRIMARY KEY ([Id])
    );
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigitalStorageAuditResult",
                schema: "Ebook");

            migrationBuilder.DropColumn(
                name: "Capacity",
                schema: "PrintBook",
                table: "Store");
        }
    }
}
