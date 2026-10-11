using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BibCover : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_url",
                table: "bibs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_url",
                table: "bibs");
        }
    }
}
