using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBarcodeTenantDateIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Barcode_TenantId_CreatedRowDate' AND object_id = OBJECT_ID('PrintBook.Barcode'))
                CREATE NONCLUSTERED INDEX IX_Barcode_TenantId_CreatedRowDate ON PrintBook.Barcode (TenantId, CreatedRowDate) INCLUDE (Store, BibId, IsDelete);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Barcode_TenantId_CreatedRowDate' AND object_id = OBJECT_ID('PrintBook.Barcode'))
                DROP INDEX IX_Barcode_TenantId_CreatedRowDate ON PrintBook.Barcode;
            ");
        }
    }
}
