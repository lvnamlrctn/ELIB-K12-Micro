using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Holdings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShelfLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "shelf_public_id",
                table: "items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "shelf_row_public_id",
                table: "items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "shelved_at",
                table: "items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "floor_replicas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    floor_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    floor_number = table.Column<int>(type: "integer", nullable: true),
                    building_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    canvas_width = table.Column<double>(type: "double precision", nullable: false),
                    canvas_height = table.Column<double>(type: "double precision", nullable: false),
                    layout_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    source_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_floor_replicas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_movements",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    from_store_id = table.Column<long>(type: "bigint", nullable: true),
                    to_store_id = table.Column<long>(type: "bigint", nullable: true),
                    from_shelf_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_shelf_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_row_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_row_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    to_status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    actor_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_item_movements_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shelf_replicas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    shelf_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    floor_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    store_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position_x = table.Column<double>(type: "double precision", nullable: true),
                    position_y = table.Column<double>(type: "double precision", nullable: true),
                    width = table.Column<double>(type: "double precision", nullable: true),
                    height = table.Column<double>(type: "double precision", nullable: true),
                    color_hex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    source_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shelf_replicas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shelf_row_replicas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    shelf_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_index = table.Column<int>(type: "integer", nullable: false),
                    ddc_start = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ddc_end = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shelf_row_replicas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_items_tenant_id_shelf_public_id",
                table: "items",
                columns: new[] { "tenant_id", "shelf_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_floor_replicas_tenant_id",
                table: "floor_replicas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_floor_replicas_tenant_id_floor_public_id",
                table: "floor_replicas",
                columns: new[] { "tenant_id", "floor_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_movements_item_id",
                table: "item_movements",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_movements_tenant_id",
                table: "item_movements",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_movements_tenant_id_item_id_at",
                table: "item_movements",
                columns: new[] { "tenant_id", "item_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_shelf_replicas_tenant_id",
                table: "shelf_replicas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shelf_replicas_tenant_id_shelf_public_id",
                table: "shelf_replicas",
                columns: new[] { "tenant_id", "shelf_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shelf_replicas_tenant_id_store_public_id",
                table: "shelf_replicas",
                columns: new[] { "tenant_id", "store_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_shelf_row_replicas_tenant_id",
                table: "shelf_row_replicas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shelf_row_replicas_tenant_id_shelf_public_id",
                table: "shelf_row_replicas",
                columns: new[] { "tenant_id", "shelf_public_id" });

            foreach (var table in new[] { "floor_replicas", "item_movements", "shelf_replicas", "shelf_row_replicas" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "shelf_row_replicas", "shelf_replicas", "item_movements", "floor_replicas" })
                migrationBuilder.DisableTenantRls(table);

            migrationBuilder.DropTable(
                name: "floor_replicas");

            migrationBuilder.DropTable(
                name: "item_movements");

            migrationBuilder.DropTable(
                name: "shelf_replicas");

            migrationBuilder.DropTable(
                name: "shelf_row_replicas");

            migrationBuilder.DropIndex(
                name: "ix_items_tenant_id_shelf_public_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "shelf_public_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "shelf_row_public_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "shelved_at",
                table: "items");
        }
    }
}
