using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoansAndReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ebook_item_loans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_access_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    recalled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recalled_by = table.Column<long>(type: "bigint", nullable: true),
                    recall_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_ebook_item_loans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_item_reservations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ready_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fulfilled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_ebook_item_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_loans_public_id",
                table: "ebook_item_loans",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_loans_tenant_id",
                table: "ebook_item_loans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_loans_tenant_id_ebook_item_id_status",
                table: "ebook_item_loans",
                columns: new[] { "tenant_id", "ebook_item_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_loans_tenant_id_reader_public_id_status",
                table: "ebook_item_loans",
                columns: new[] { "tenant_id", "reader_public_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_reservations_public_id",
                table: "ebook_item_reservations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_reservations_tenant_id",
                table: "ebook_item_reservations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_reservations_tenant_id_ebook_item_id_status",
                table: "ebook_item_reservations",
                columns: new[] { "tenant_id", "ebook_item_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_item_reservations_tenant_id_reader_public_id_status",
                table: "ebook_item_reservations",
                columns: new[] { "tenant_id", "reader_public_id", "status" });

            // Lớp bảo vệ thứ hai (docs 04 §2.1): mọi bảng của đơn vị bật RLS.
            migrationBuilder.EnableTenantRls("ebook_item_loans");
            migrationBuilder.EnableTenantRls("ebook_item_reservations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("ebook_item_reservations");
            migrationBuilder.DisableTenantRls("ebook_item_loans");

            migrationBuilder.DropTable(
                name: "ebook_item_loans");

            migrationBuilder.DropTable(
                name: "ebook_item_reservations");
        }
    }
}
