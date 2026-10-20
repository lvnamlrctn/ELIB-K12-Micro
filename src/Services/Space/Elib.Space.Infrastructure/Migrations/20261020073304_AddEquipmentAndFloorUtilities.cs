using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentAndFloorUtilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "map_fixture_equipment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fixture_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    condition_status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_map_fixture_equipment", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_fixture_equipment_map_fixtures_fixture_id",
                        column: x => x.fixture_id,
                        principalTable: "map_fixtures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_floor_utilities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    floor_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: true),
                    condition_status = table.Column<int>(type: "integer", nullable: false),
                    icon_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("pk_map_floor_utilities", x => x.id);
                    table.ForeignKey(
                        name: "fk_map_floor_utilities_map_floors_floor_id",
                        column: x => x.floor_id,
                        principalTable: "map_floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_map_fixture_equipment_fixture_id",
                table: "map_fixture_equipment",
                column: "fixture_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_fixture_equipment_public_id",
                table: "map_fixture_equipment",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_fixture_equipment_tenant_id",
                table: "map_fixture_equipment",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_fixture_equipment_tenant_id_fixture_id",
                table: "map_fixture_equipment",
                columns: new[] { "tenant_id", "fixture_id" });

            migrationBuilder.CreateIndex(
                name: "ix_map_floor_utilities_floor_id",
                table: "map_floor_utilities",
                column: "floor_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_floor_utilities_public_id",
                table: "map_floor_utilities",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_map_floor_utilities_tenant_id",
                table: "map_floor_utilities",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_floor_utilities_tenant_id_floor_id",
                table: "map_floor_utilities",
                columns: new[] { "tenant_id", "floor_id" });

            migrationBuilder.EnableTenantRls("map_fixture_equipment");
            migrationBuilder.EnableTenantRls("map_floor_utilities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "map_fixture_equipment");

            migrationBuilder.DropTable(
                name: "map_floor_utilities");
        }
    }
}
