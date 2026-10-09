using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Currency] WHERE Code = 'VND')
                INSERT INTO [dbo].[Currency] (Code, Name, ExchangeRate, Status, IsDelete, PublicId)
                VALUES ('VND', N'Việt Nam Đồng', 1, 2, 0, NEWID());

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Currency] WHERE Code = 'USD')
                INSERT INTO [dbo].[Currency] (Code, Name, ExchangeRate, Status, IsDelete, PublicId)
                VALUES ('USD', N'Đô la Mỹ', 25000, 2, 0, NEWID());

                IF NOT EXISTS (SELECT 1 FROM [dbo].[Currency] WHERE Code = 'EUR')
                INSERT INTO [dbo].[Currency] (Code, Name, ExchangeRate, Status, IsDelete, PublicId)
                VALUES ('EUR', N'Euro', 27000, 2, 0, NEWID());
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM [dbo].[Currency] WHERE Code IN ('VND', 'USD', 'EUR');
            ");
        }
    }
}
