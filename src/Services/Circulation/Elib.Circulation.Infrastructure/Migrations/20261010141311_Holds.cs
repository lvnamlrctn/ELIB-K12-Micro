using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Holds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "hold_days",
                table: "loan_policies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "max_holds",
                table: "loan_policies",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "holds",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mfn = table.Column<long>(type: "bigint", nullable: false),
                    circ_place_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    item_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_holds", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_holds_public_id",
                table: "holds",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_holds_tenant_id",
                table: "holds",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_holds_tenant_id_item_public_id",
                table: "holds",
                columns: new[] { "tenant_id", "item_public_id" },
                unique: true,
                filter: "status = 2 AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_holds_tenant_id_mfn_status_requested_at",
                table: "holds",
                columns: new[] { "tenant_id", "mfn", "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_holds_tenant_id_reader_public_id_status",
                table: "holds",
                columns: new[] { "tenant_id", "reader_public_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_holds_tenant_id_status_expires_at",
                table: "holds",
                columns: new[] { "tenant_id", "status", "expires_at" });

            migrationBuilder.EnableTenantRls("holds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holds");

            migrationBuilder.DropColumn(
                name: "hold_days",
                table: "loan_policies");

            migrationBuilder.DropColumn(
                name: "max_holds",
                table: "loan_policies");
        }
    }
}
