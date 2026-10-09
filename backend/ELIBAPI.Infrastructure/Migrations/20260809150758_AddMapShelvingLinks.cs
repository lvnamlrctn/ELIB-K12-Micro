using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMapShelvingLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "StoreId",
                schema: "map",
                table: "MapObject",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MapObjectId",
                schema: "PrintBook",
                table: "Barcode",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MapShelfRowId",
                schema: "PrintBook",
                table: "Barcode",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoreId",
                schema: "map",
                table: "MapObject");

            migrationBuilder.DropColumn(
                name: "MapObjectId",
                schema: "PrintBook",
                table: "Barcode");

            migrationBuilder.DropColumn(
                name: "MapShelfRowId",
                schema: "PrintBook",
                table: "Barcode");
        }
    }
}
