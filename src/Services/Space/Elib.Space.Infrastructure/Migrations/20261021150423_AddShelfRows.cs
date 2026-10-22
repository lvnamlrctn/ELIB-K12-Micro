using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShelfRows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shelf_rows",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    row_index = table.Column<int>(type: "integer", nullable: false),
                    ddc_start = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ddc_end = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shelf_rows", x => x.id);
                    table.ForeignKey(
                        name: "fk_shelf_rows_map_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "map_fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shelf_rows_fixture_id",
                table: "shelf_rows",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "ix_shelf_rows_public_id",
                table: "shelf_rows",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shelf_rows_tenant_id",
                table: "shelf_rows",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shelf_rows_tenant_id_fixture_id_row_index",
                table: "shelf_rows",
                columns: new[] { "tenant_id", "fixture_id", "row_index" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.EnableTenantRls("shelf_rows");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("shelf_rows");

            migrationBuilder.DropTable(
                name: "shelf_rows");
        }
    }
}
