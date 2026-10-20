using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "access_devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    space_id = table.Column<long>(type: "bigint", nullable: true),
                    api_key_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_access_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_access_devices_bookable_spaces_space_id",
                        column: x => x.space_id,
                        principalTable: "bookable_spaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "access_staff_cards",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    card_uid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("pk_access_staff_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "access_commands",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    command = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    acked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_access_commands", x => x.id);
                    table.ForeignKey(
                        name: "fk_access_commands_access_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "access_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "access_scan_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    card_uid = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    booking_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    allowed = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    scanned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_access_scan_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_access_scan_logs_access_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "access_devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_commands_device_id",
                table: "access_commands",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_commands_public_id",
                table: "access_commands",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_commands_tenant_id",
                table: "access_commands",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_commands_tenant_id_device_id_acked_at_expires_at",
                table: "access_commands",
                columns: new[] { "tenant_id", "device_id", "acked_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_access_devices_public_id",
                table: "access_devices",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_devices_space_id",
                table: "access_devices",
                column: "space_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_devices_tenant_id",
                table: "access_devices",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_devices_tenant_id_code",
                table: "access_devices",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_access_scan_logs_device_id",
                table: "access_scan_logs",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_scan_logs_public_id",
                table: "access_scan_logs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_scan_logs_tenant_id",
                table: "access_scan_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_scan_logs_tenant_id_device_id_scanned_at",
                table: "access_scan_logs",
                columns: new[] { "tenant_id", "device_id", "scanned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_access_staff_cards_public_id",
                table: "access_staff_cards",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_staff_cards_tenant_id",
                table: "access_staff_cards",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_staff_cards_tenant_id_card_uid",
                table: "access_staff_cards",
                columns: new[] { "tenant_id", "card_uid" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.EnableTenantRls("access_devices");
            migrationBuilder.EnableTenantRls("access_staff_cards");
            migrationBuilder.EnableTenantRls("access_commands");
            migrationBuilder.EnableTenantRls("access_scan_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_commands");

            migrationBuilder.DropTable(
                name: "access_scan_logs");

            migrationBuilder.DropTable(
                name: "access_staff_cards");

            migrationBuilder.DropTable(
                name: "access_devices");
        }
    }
}
