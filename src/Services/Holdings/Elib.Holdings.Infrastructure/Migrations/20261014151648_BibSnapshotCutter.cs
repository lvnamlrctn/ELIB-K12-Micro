using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Holdings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BibSnapshotCutter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cutter",
                table: "bib_snapshots",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "cutter_known",
                table: "bib_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cutter",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "cutter_known",
                table: "bib_snapshots");
        }
    }
}
