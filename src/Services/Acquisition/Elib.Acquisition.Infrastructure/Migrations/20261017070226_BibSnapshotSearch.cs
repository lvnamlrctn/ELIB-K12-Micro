using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Acquisition.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BibSnapshotSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "author_key",
                table: "bib_snapshots",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "bib_type_id",
                table: "bib_snapshots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bib_type_name",
                table: "bib_snapshots",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "publisher_key",
                table: "bib_snapshots",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "synced",
                table: "bib_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "title_key",
                table: "bib_snapshots",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_bib_snapshots_tenant_id_synced",
                table: "bib_snapshots",
                columns: new[] { "tenant_id", "synced" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bib_snapshots_tenant_id_synced",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "author_key",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "bib_type_id",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "bib_type_name",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "publisher_key",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "synced",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "title_key",
                table: "bib_snapshots");
        }
    }
}
