using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexBibDataFieldSubFieldData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phục vụ CheckIsbn (cảnh báo trùng ISBN) + các truy vấn lọc theo Field/SubField/Data khác
            // (VD BookController.cs tra theo 520$a) — trước đây chỉ có index (BibId, Field), quét toàn bảng
            // Field/SubField/Data mất 15-25s trên 234K dòng.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BibData_Field_SubField_Data' AND object_id = OBJECT_ID('PrintBook.BibData'))
                CREATE NONCLUSTERED INDEX IX_BibData_Field_SubField_Data ON PrintBook.BibData (Field, SubField, Data) INCLUDE (BibId, IsDelete);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BibData_Field_SubField_Data' AND object_id = OBJECT_ID('PrintBook.BibData'))
                DROP INDEX IX_BibData_Field_SubField_Data ON PrintBook.BibData;
            ");
        }
    }
}
