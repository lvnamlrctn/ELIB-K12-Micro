using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "library_visits",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    circ_place_id = table.Column<long>(type: "bigint", nullable: false),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    checked_in_by = table.Column<long>(type: "bigint", nullable: true),
                    checked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    circ_place_out_id = table.Column<long>(type: "bigint", nullable: true),
                    checked_out_by = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("pk_library_visits", x => x.id);
                    table.ForeignKey(
                        name: "fk_library_visits_circ_places_circ_place_id",
                        column: x => x.circ_place_id,
                        principalTable: "circ_places",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_library_visits_circ_places_circ_place_out_id",
                        column: x => x.circ_place_out_id,
                        principalTable: "circ_places",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_library_visits_circ_place_id",
                table: "library_visits",
                column: "circ_place_id");

            migrationBuilder.CreateIndex(
                name: "ix_library_visits_circ_place_out_id",
                table: "library_visits",
                column: "circ_place_out_id");

            migrationBuilder.CreateIndex(
                name: "ix_library_visits_public_id",
                table: "library_visits",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_library_visits_tenant_id",
                table: "library_visits",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_library_visits_tenant_id_card_no",
                table: "library_visits",
                columns: new[] { "tenant_id", "card_no" });

            migrationBuilder.CreateIndex(
                name: "ix_library_visits_tenant_id_checked_in_at",
                table: "library_visits",
                columns: new[] { "tenant_id", "checked_in_at" });

            migrationBuilder.CreateIndex(
                name: "ix_library_visits_tenant_id_reader_public_id",
                table: "library_visits",
                columns: new[] { "tenant_id", "reader_public_id" },
                unique: true,
                filter: "checked_out_at IS NULL AND is_deleted = false");

            migrationBuilder.EnableTenantRls("library_visits");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "library_visits");
        }
    }
}
