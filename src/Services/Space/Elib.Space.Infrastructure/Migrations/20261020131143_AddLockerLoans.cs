using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLockerLoans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locker_loans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    compartment_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    borrowed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    borrowed_by = table.Column<long>(type: "bigint", nullable: true),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    returned_by = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("pk_locker_loans", x => x.id);
                    table.ForeignKey(
                        name: "fk_locker_loans_locker_compartments_compartment_id",
                        column: x => x.compartment_id,
                        principalTable: "locker_compartments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_locker_loans_compartment_id",
                table: "locker_loans",
                column: "compartment_id");

            migrationBuilder.CreateIndex(
                name: "ix_locker_loans_public_id",
                table: "locker_loans",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locker_loans_tenant_id",
                table: "locker_loans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_locker_loans_tenant_id_card_no",
                table: "locker_loans",
                columns: new[] { "tenant_id", "card_no" });

            migrationBuilder.CreateIndex(
                name: "ix_locker_loans_tenant_id_compartment_id_returned_at",
                table: "locker_loans",
                columns: new[] { "tenant_id", "compartment_id", "returned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_locker_loans_tenant_id_reader_public_id",
                table: "locker_loans",
                columns: new[] { "tenant_id", "reader_public_id" });

            migrationBuilder.EnableTenantRls("locker_loans");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("locker_loans");

            migrationBuilder.DropTable(
                name: "locker_loans");
        }
    }
}
