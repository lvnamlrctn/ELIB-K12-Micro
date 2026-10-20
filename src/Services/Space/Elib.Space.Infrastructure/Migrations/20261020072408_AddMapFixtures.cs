using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMapFixtures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_fixtures",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    floor_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    object_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    position_x = table.Column<double>(type: "double precision", nullable: true),
                    position_y = table.Column<double>(type: "double precision", nullable: true),
                    width = table.Column<double>(type: "double precision", nullable: true),
                    height = table.Column<double>(type: "double precision", nullable: true),
                    color_hex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    icon_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    shelf_category_range = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    shelf_subject_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    shelf_capacity = table.Column<int>(type: "integer", nullable: true),
                    shelf_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    store_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_map_fixtures", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_fixtures_map_floors_floor_id",
                        column: x => x.floor_id,
                        principalTable: "map_floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_fixtures_floor_id",
                table: "map_fixtures",
                column: "floor_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_fixtures_public_id",
                table: "map_fixtures",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_fixtures_tenant_id",
                table: "map_fixtures",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_fixtures_tenant_id_code",
                table: "map_fixtures",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_map_fixtures_tenant_id_floor_id",
                table: "map_fixtures",
                columns: new[] { "tenant_id", "floor_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_fixtures");
        }
    }
}
