using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Acquisition.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RatesPaymentsCatalogedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "payment_method",
                table: "receipts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "payment_status",
                table: "receipts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "rate",
                table: "receipt_lines",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<int>(
                name: "payment_method",
                table: "purchase_orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "rate",
                table: "order_lines",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cataloged_at",
                table: "bib_snapshots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_bib_snapshots_tenant_id_cataloged_at",
                table: "bib_snapshots",
                columns: new[] { "tenant_id", "cataloged_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bib_snapshots_tenant_id_cataloged_at",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "receipts");

            migrationBuilder.DropColumn(
                name: "rate",
                table: "receipt_lines");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "purchase_orders");

            migrationBuilder.DropColumn(
                name: "rate",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "cataloged_at",
                table: "bib_snapshots");
        }
    }
}
