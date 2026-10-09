using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReaderLockReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LockReason",
                schema: "dbo",
                table: "Reader",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockReason",
                schema: "dbo",
                table: "Reader");
        }
    }
}
