using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Acquisition.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBibSnapshotClassification : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "ddc",
                table: "bib_snapshots",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cutter",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "ddc",
                table: "bib_snapshots");
        }
    }
}
