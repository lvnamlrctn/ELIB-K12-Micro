using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhotocopiesRenewals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loan_renewals",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    loan_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    circ_place_id = table.Column<long>(type: "bigint", nullable: true),
                    renewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    old_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    new_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    renewed_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_loan_renewals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "photocopies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mfn = table.Column<long>(type: "bigint", nullable: false),
                    from_page = table.Column<int>(type: "integer", nullable: false),
                    to_page = table.Column<int>(type: "integer", nullable: false),
                    copies = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    paid = table.Column<bool>(type: "boolean", nullable: false),
                    photo_date = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("pk_photocopies", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_loan_renewals_tenant_id",
                table: "loan_renewals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_renewals_tenant_id_loan_public_id",
                table: "loan_renewals",
                columns: new[] { "tenant_id", "loan_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_loan_renewals_tenant_id_renewed_at",
                table: "loan_renewals",
                columns: new[] { "tenant_id", "renewed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_photocopies_public_id",
                table: "photocopies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photocopies_tenant_id",
                table: "photocopies",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_photocopies_tenant_id_card_no",
                table: "photocopies",
                columns: new[] { "tenant_id", "card_no" });

            migrationBuilder.CreateIndex(
                name: "ix_photocopies_tenant_id_photo_date",
                table: "photocopies",
                columns: new[] { "tenant_id", "photo_date" });

            foreach (var table in new[] { "loan_renewals", "photocopies" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loan_renewals");

            migrationBuilder.DropTable(
                name: "photocopies");
        }
    }
}
