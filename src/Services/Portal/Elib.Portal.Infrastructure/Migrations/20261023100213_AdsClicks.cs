using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Portal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdsClicks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "clicks",
                table: "ads",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "clicks",
                table: "ads");
        }
    }
}
