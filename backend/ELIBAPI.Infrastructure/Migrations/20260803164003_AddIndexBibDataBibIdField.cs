using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexBibDataBibIdField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BibData_BibId_Field' AND object_id = OBJECT_ID('PrintBook.BibData'))
                CREATE NONCLUSTERED INDEX IX_BibData_BibId_Field ON PrintBook.BibData (BibId, Field) INCLUDE (SubField, IsDelete);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BibData_BibId_Field' AND object_id = OBJECT_ID('PrintBook.BibData'))
                DROP INDEX IX_BibData_BibId_Field ON PrintBook.BibData;
            ");
        }
    }
}
