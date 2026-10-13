using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CircPlaceLrcFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "circ_places",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "max_loans",
                table: "circ_places",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "circ_places",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<List<long>>(
                name: "reader_type_ids",
                table: "circ_places",
                type: "bigint[]",
                nullable: false,
                defaultValueSql: "'{}'::bigint[]");

            migrationBuilder.AddColumn<string>(
                name: "work_sessions",
                table: "circ_places",
                type: "jsonb",
                nullable: true,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kind",
                table: "circ_places");

            migrationBuilder.DropColumn(
                name: "max_loans",
                table: "circ_places");

            migrationBuilder.DropColumn(
                name: "note",
                table: "circ_places");

            migrationBuilder.DropColumn(
                name: "reader_type_ids",
                table: "circ_places");

            migrationBuilder.DropColumn(
                name: "work_sessions",
                table: "circ_places");
        }
    }
}
