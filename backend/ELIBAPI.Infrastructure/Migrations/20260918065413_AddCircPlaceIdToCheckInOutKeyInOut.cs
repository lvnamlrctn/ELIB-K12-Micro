using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCircPlaceIdToCheckInOutKeyInOut : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "KeyOut",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "KeyIn",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "CheckOut",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "CheckIn",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "KeyOut");

            migrationBuilder.DropColumn(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "KeyIn");

            migrationBuilder.DropColumn(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "CheckOut");

            migrationBuilder.DropColumn(
                name: "CircPlaceId",
                schema: "PrintBook",
                table: "CheckIn");
        }
    }
}
