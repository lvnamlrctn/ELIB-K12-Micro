using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCPhotoPaidAndTotal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IsPaid",
                schema: "PrintBook",
                table: "C_photo",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalAmount",
                schema: "PrintBook",
                table: "C_photo",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaid",
                schema: "PrintBook",
                table: "C_photo");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                schema: "PrintBook",
                table: "C_photo");
        }
    }
}
