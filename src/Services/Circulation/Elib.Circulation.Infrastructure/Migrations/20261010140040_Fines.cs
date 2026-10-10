using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "patron_replicas",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "patron_replicas",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_item_status",
                table: "loans",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "due_soon_notified_on",
                table: "loans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "overdue_notified_on",
                table: "loans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "fine_per_day",
                table: "loan_policies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "renew_from_today",
                table: "loan_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "fine_reasons",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("pk_fine_reasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fine_tickets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    number = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fine_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    round = table.Column<int>(type: "integer", nullable: false),
                    manual_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    remaining = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_fine_tickets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fine_lines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    fine_ticket_id = table.Column<long>(type: "bigint", nullable: false),
                    reason_id = table.Column<long>(type: "bigint", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    loan_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    mfn = table.Column<long>(type: "bigint", nullable: true),
                    overdue_days = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fine_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_fine_lines_fine_tickets_fine_ticket_id",
                        column: x => x.fine_ticket_id,
                        principalTable: "fine_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_fine_lines_fine_ticket_id",
                table: "fine_lines",
                column: "fine_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_fine_lines_tenant_id",
                table: "fine_lines",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fine_lines_tenant_id_loan_public_id",
                table: "fine_lines",
                columns: new[] { "tenant_id", "loan_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fine_reasons_public_id",
                table: "fine_reasons",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fine_reasons_tenant_id",
                table: "fine_reasons",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fine_reasons_tenant_id_code",
                table: "fine_reasons",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_fine_tickets_public_id",
                table: "fine_tickets",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fine_tickets_tenant_id",
                table: "fine_tickets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fine_tickets_tenant_id_fine_date",
                table: "fine_tickets",
                columns: new[] { "tenant_id", "fine_date" });

            migrationBuilder.CreateIndex(
                name: "ix_fine_tickets_tenant_id_number",
                table: "fine_tickets",
                columns: new[] { "tenant_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fine_tickets_tenant_id_reader_public_id_status",
                table: "fine_tickets",
                columns: new[] { "tenant_id", "reader_public_id", "status" });

            foreach (var table in new[] { "fine_reasons", "fine_tickets", "fine_lines" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fine_lines");

            migrationBuilder.DropTable(
                name: "fine_reasons");

            migrationBuilder.DropTable(
                name: "fine_tickets");

            migrationBuilder.DropColumn(
                name: "email",
                table: "patron_replicas");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "patron_replicas");

            migrationBuilder.DropColumn(
                name: "closed_item_status",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "due_soon_notified_on",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "overdue_notified_on",
                table: "loans");

            migrationBuilder.DropColumn(
                name: "fine_per_day",
                table: "loan_policies");

            migrationBuilder.DropColumn(
                name: "renew_from_today",
                table: "loan_policies");
        }
    }
}
