using Elib.BuildingBlocks.Persistence;
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Holdings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "receipt_line_public_id",
                table: "items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receipt_public_id",
                table: "items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "receipt_public_id",
                table: "deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "receipt_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    receipt_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    received_on = table.Column<DateOnly>(type: "date", nullable: true),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    registration_stamp = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_snapshots", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_items_tenant_id_receipt_line_public_id",
                table: "items",
                columns: new[] { "tenant_id", "receipt_line_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_items_tenant_id_receipt_public_id",
                table: "items",
                columns: new[] { "tenant_id", "receipt_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_deliveries_tenant_id_receipt_public_id",
                table: "deliveries",
                columns: new[] { "tenant_id", "receipt_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_snapshots_tenant_id",
                table: "receipt_snapshots",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipt_snapshots_tenant_id_code",
                table: "receipt_snapshots",
                columns: new[] { "tenant_id", "code" });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_snapshots_tenant_id_receipt_public_id",
                table: "receipt_snapshots",
                columns: new[] { "tenant_id", "receipt_public_id" },
                unique: true);

            // Lớp bảo vệ thứ hai (docs 04 §2.1): mọi bảng của đơn vị bật RLS.
            migrationBuilder.EnableTenantRls("receipt_snapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_snapshots");

            migrationBuilder.DropIndex(
                name: "ix_items_tenant_id_receipt_line_public_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "ix_items_tenant_id_receipt_public_id",
                table: "items");

            migrationBuilder.DropIndex(
                name: "ix_deliveries_tenant_id_receipt_public_id",
                table: "deliveries");

            migrationBuilder.DropColumn(
                name: "receipt_line_public_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "receipt_public_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "receipt_public_id",
                table: "deliveries");
        }
    }
}
