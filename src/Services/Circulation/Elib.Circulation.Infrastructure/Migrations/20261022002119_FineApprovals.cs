using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FineApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "fine_tickets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "approval_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    amount_threshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    percent_threshold = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    count_threshold = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_approval_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "change_requests",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_label = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    before_json = table.Column<string>(type: "text", nullable: true),
                    after_json = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    count = table.Column<int>(type: "integer", nullable: true),
                    target_version = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by = table.Column<long>(type: "bigint", nullable: true),
                    requested_by_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<long>(type: "bigint", nullable: true),
                    decided_by_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_change_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fine_payments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fine_ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    by_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    external_ref = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_fine_payments", x => x.id);
                    table.ForeignKey(
                        name: "fk_fine_payments_fine_tickets_fine_ticket_id",
                        column: x => x.fine_ticket_id,
                        principalTable: "fine_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_rules_public_id",
                table: "approval_rules",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_rules_tenant_id",
                table: "approval_rules",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_rules_tenant_id_kind",
                table: "approval_rules",
                columns: new[] { "tenant_id", "kind" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_public_id",
                table: "change_requests",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_tenant_id",
                table: "change_requests",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_tenant_id_requested_by",
                table: "change_requests",
                columns: new[] { "tenant_id", "requested_by" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_tenant_id_status_kind",
                table: "change_requests",
                columns: new[] { "tenant_id", "status", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_change_requests_tenant_id_target_public_id",
                table: "change_requests",
                columns: new[] { "tenant_id", "target_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fine_payments_fine_ticket_id",
                table: "fine_payments",
                column: "fine_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_fine_payments_public_id",
                table: "fine_payments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fine_payments_tenant_id",
                table: "fine_payments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fine_payments_tenant_id_external_ref",
                table: "fine_payments",
                columns: new[] { "tenant_id", "external_ref" },
                unique: true,
                filter: "external_ref IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_fine_payments_tenant_id_fine_ticket_id",
                table: "fine_payments",
                columns: new[] { "tenant_id", "fine_ticket_id" });

            // Số đã thu trước đây chỉ là một con số trên phiếu: chuyển thành một khoản "số dư chuyển sang" để Paid = tổng các khoản thanh toán
            // mà phiếu cũ không bị coi là chưa thu. Bảng có FORCE RLS → bật ngữ cảnh hệ thống trong transaction của migration.
            migrationBuilder.Sql("""
                SELECT set_config('app.tenant_system', 'on', true);
                INSERT INTO fine_payments (tenant_id, public_id, created_at, is_deleted, fine_ticket_id, amount, method, note, paid_at)
                SELECT tenant_id, gen_random_uuid(), now(), false, id, paid, 'CARRY', 'Số đã thu trước khi có sổ thanh toán', COALESCE(updated_at, created_at)
                FROM fine_tickets WHERE paid > 0 AND is_deleted = false;
                """);

            migrationBuilder.EnableTenantRls("fine_payments");
            migrationBuilder.EnableTenantRls("change_requests");
            migrationBuilder.EnableTenantRls("approval_rules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_rules");

            migrationBuilder.DropTable(
                name: "change_requests");

            migrationBuilder.DropTable(
                name: "fine_payments");

            migrationBuilder.DropColumn(
                name: "version",
                table: "fine_tickets");
        }
    }
}
