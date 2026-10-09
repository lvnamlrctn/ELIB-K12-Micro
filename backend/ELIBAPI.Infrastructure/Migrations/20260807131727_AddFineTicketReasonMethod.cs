using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFineTicketReasonMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FineMethodId",
                schema: "PrintBook",
                table: "C_Fine_Ticket",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FineTypeId",
                schema: "PrintBook",
                table: "C_Fine_Ticket",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FineMethodId",
                schema: "PrintBook",
                table: "C_Fine_Ticket");

            migrationBuilder.DropColumn(
                name: "FineTypeId",
                schema: "PrintBook",
                table: "C_Fine_Ticket");
        }
    }
}
