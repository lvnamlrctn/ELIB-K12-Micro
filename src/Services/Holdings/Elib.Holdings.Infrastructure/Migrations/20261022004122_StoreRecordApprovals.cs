using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Holdings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StoreRecordApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "previous_status",
                table: "lost_records",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversal_reason",
                table: "lost_records",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversal_request_id",
                table: "lost_records",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reversed_at",
                table: "lost_records",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversed_by_name",
                table: "lost_records",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "decision_date",
                table: "liquidations",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "decision_no",
                table: "liquidations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "previous_status",
                table: "liquidations",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversal_reason",
                table: "liquidations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reversal_request_id",
                table: "liquidations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reversed_at",
                table: "liquidations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reversed_by_name",
                table: "liquidations",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "previous_status",
                table: "lost_records");

            migrationBuilder.DropColumn(
                name: "reversal_reason",
                table: "lost_records");

            migrationBuilder.DropColumn(
                name: "reversal_request_id",
                table: "lost_records");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "lost_records");

            migrationBuilder.DropColumn(
                name: "reversed_by_name",
                table: "lost_records");

            migrationBuilder.DropColumn(
                name: "decision_date",
                table: "liquidations");

            migrationBuilder.DropColumn(
                name: "decision_no",
                table: "liquidations");

            migrationBuilder.DropColumn(
                name: "previous_status",
                table: "liquidations");

            migrationBuilder.DropColumn(
                name: "reversal_reason",
                table: "liquidations");

            migrationBuilder.DropColumn(
                name: "reversal_request_id",
                table: "liquidations");

            migrationBuilder.DropColumn(
                name: "reversed_at",
                table: "liquidations");

            migrationBuilder.DropColumn(
                name: "reversed_by_name",
                table: "liquidations");
        }
    }
}
