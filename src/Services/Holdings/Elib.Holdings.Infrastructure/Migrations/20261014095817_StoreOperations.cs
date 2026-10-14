using Elib.BuildingBlocks.Persistence;
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Holdings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "book_out_units",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_book_out_units", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deliveries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    deliverer_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    deliverer_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    receiver_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    receiver_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    delivered_on = table.Column<DateOnly>(type: "date", nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: true),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    shelved_count = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_deliveries_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "exhibition_locations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exhibition_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "export_reasons",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_export_reasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inventories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    started_on = table.Column<DateOnly>(type: "date", nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_inventories", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventories_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "liquidations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    liquidated_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_liquidations", x => x.id);
                    table.ForeignKey(
                        name: "fk_liquidations_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lost_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    lost_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("pk_lost_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_lost_records_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_moves",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    from_store_id = table.Column<long>(type: "bigint", nullable: false),
                    to_store_id = table.Column<long>(type: "bigint", nullable: false),
                    deliverer_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    receiver_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    moved_on = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    moved_count = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_store_moves", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_moves_stores_from_store_id",
                        column: x => x.from_store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_moves_stores_to_store_id",
                        column: x => x.to_store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    delivery_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_delivery_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_delivery_lines_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_delivery_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_outs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    reason_id = table.Column<long>(type: "bigint", nullable: true),
                    unit_id = table.Column<long>(type: "bigint", nullable: true),
                    exhibition_location_id = table.Column<long>(type: "bigint", nullable: true),
                    deliverer_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    receiver_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    return_at_library = table.Column<bool>(type: "boolean", nullable: false),
                    out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_store_outs", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_outs_book_out_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "book_out_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_outs_exhibition_locations_exhibition_location_id",
                        column: x => x.exhibition_location_id,
                        principalTable: "exhibition_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_outs_export_reasons_reason_id",
                        column: x => x.reason_id,
                        principalTable: "export_reasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_outs_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_scans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inventory_id = table.Column<long>(type: "bigint", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    barcode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: true),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    scan_store_id = table.Column<long>(type: "bigint", nullable: true),
                    registered = table.Column<bool>(type: "boolean", nullable: false),
                    wrong_store = table.Column<bool>(type: "boolean", nullable: false),
                    on_loan = table.Column<bool>(type: "boolean", nullable: false),
                    item_status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
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
                    table.PrimaryKey("pk_inventory_scans", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_scans_inventories_inventory_id",
                        column: x => x.inventory_id,
                        principalTable: "inventories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "store_move_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    move_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    skip_reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
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
                    table.PrimaryKey("pk_store_move_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_store_move_lines_items_item_id",
                        column: x => x.item_id,
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_store_move_lines_store_moves_move_id",
                        column: x => x.move_id,
                        principalTable: "store_moves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_book_out_units_public_id",
                table: "book_out_units",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_book_out_units_tenant_id",
                table: "book_out_units",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_out_units_tenant_id_name",
                table: "book_out_units",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_public_id",
                table: "deliveries",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_store_id",
                table: "deliveries",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_tenant_id",
                table: "deliveries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_lines_delivery_id",
                table: "delivery_lines",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_lines_item_id",
                table: "delivery_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_lines_public_id",
                table: "delivery_lines",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_lines_tenant_id",
                table: "delivery_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_lines_tenant_id_delivery_id",
                table: "delivery_lines",
                columns: new[] { "tenant_id", "delivery_id" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_lines_tenant_id_item_id",
                table: "delivery_lines",
                columns: new[] { "tenant_id", "item_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_exhibition_locations_public_id",
                table: "exhibition_locations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exhibition_locations_tenant_id",
                table: "exhibition_locations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_exhibition_locations_tenant_id_name",
                table: "exhibition_locations",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_export_reasons_public_id",
                table: "export_reasons",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_export_reasons_tenant_id",
                table: "export_reasons",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_export_reasons_tenant_id_name",
                table: "export_reasons",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_inventories_public_id",
                table: "inventories",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventories_store_id",
                table: "inventories",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventories_tenant_id",
                table: "inventories",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_scans_inventory_id",
                table: "inventory_scans",
                column: "inventory_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_scans_public_id",
                table: "inventory_scans",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_scans_tenant_id",
                table: "inventory_scans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_scans_tenant_id_inventory_id_barcode_key",
                table: "inventory_scans",
                columns: new[] { "tenant_id", "inventory_id", "barcode_key" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_scans_tenant_id_inventory_id_item_id",
                table: "inventory_scans",
                columns: new[] { "tenant_id", "inventory_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_liquidations_item_id",
                table: "liquidations",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_liquidations_public_id",
                table: "liquidations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_liquidations_tenant_id",
                table: "liquidations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_liquidations_tenant_id_item_id",
                table: "liquidations",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_liquidations_tenant_id_liquidated_on",
                table: "liquidations",
                columns: new[] { "tenant_id", "liquidated_on" });

            migrationBuilder.CreateIndex(
                name: "ix_lost_records_item_id",
                table: "lost_records",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_records_public_id",
                table: "lost_records",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lost_records_tenant_id",
                table: "lost_records",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_lost_records_tenant_id_item_id",
                table: "lost_records",
                columns: new[] { "tenant_id", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_lost_records_tenant_id_lost_on",
                table: "lost_records",
                columns: new[] { "tenant_id", "lost_on" });

            migrationBuilder.CreateIndex(
                name: "ix_store_move_lines_item_id",
                table: "store_move_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_move_lines_move_id",
                table: "store_move_lines",
                column: "move_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_move_lines_public_id",
                table: "store_move_lines",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_move_lines_tenant_id",
                table: "store_move_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_move_lines_tenant_id_move_id_item_id",
                table: "store_move_lines",
                columns: new[] { "tenant_id", "move_id", "item_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_store_moves_from_store_id",
                table: "store_moves",
                column: "from_store_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_moves_public_id",
                table: "store_moves",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_moves_tenant_id",
                table: "store_moves",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_moves_to_store_id",
                table: "store_moves",
                column: "to_store_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_exhibition_location_id",
                table: "store_outs",
                column: "exhibition_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_item_id",
                table: "store_outs",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_public_id",
                table: "store_outs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_reason_id",
                table: "store_outs",
                column: "reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_tenant_id",
                table: "store_outs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_tenant_id_item_id_in_at",
                table: "store_outs",
                columns: new[] { "tenant_id", "item_id", "in_at" });

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_tenant_id_out_at",
                table: "store_outs",
                columns: new[] { "tenant_id", "out_at" });

            migrationBuilder.CreateIndex(
                name: "ix_store_outs_unit_id",
                table: "store_outs",
                column: "unit_id");
            // Lớp bảo vệ thứ hai (docs 04 §2.1): mọi bảng của đơn vị bật RLS.
            foreach (var table in new[] { "export_reasons", "book_out_units", "exhibition_locations", "lost_records", "liquidations", "store_outs",
                         "inventories", "inventory_scans", "store_moves", "store_move_lines", "deliveries", "delivery_lines" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delivery_lines");

            migrationBuilder.DropTable(
                name: "inventory_scans");

            migrationBuilder.DropTable(
                name: "liquidations");

            migrationBuilder.DropTable(
                name: "lost_records");

            migrationBuilder.DropTable(
                name: "store_move_lines");

            migrationBuilder.DropTable(
                name: "store_outs");

            migrationBuilder.DropTable(
                name: "deliveries");

            migrationBuilder.DropTable(
                name: "inventories");

            migrationBuilder.DropTable(
                name: "store_moves");

            migrationBuilder.DropTable(
                name: "book_out_units");

            migrationBuilder.DropTable(
                name: "exhibition_locations");

            migrationBuilder.DropTable(
                name: "export_reasons");

            migrationBuilder.DropColumn(
                name: "bib_type_id",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "bib_type_name",
                table: "bib_snapshots");
        }
    }
}
