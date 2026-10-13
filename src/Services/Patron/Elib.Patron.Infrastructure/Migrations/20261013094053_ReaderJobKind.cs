using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Patron.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReaderJobKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "reader_import_jobs",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Import");

            migrationBuilder.AddColumn<string>(
                name: "origin_json",
                table: "reader_import_jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "reader_import_jobs");

            migrationBuilder.DropColumn(
                name: "origin_json",
                table: "reader_import_jobs");
        }
    }
}
