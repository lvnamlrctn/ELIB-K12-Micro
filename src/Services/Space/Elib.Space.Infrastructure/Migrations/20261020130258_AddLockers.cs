using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLockers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locker_cabinets",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    floor_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    rows = table.Column<int>(type: "integer", nullable: false),
                    cols = table.Column<int>(type: "integer", nullable: false),
                    position_x = table.Column<double>(type: "double precision", nullable: true),
                    position_y = table.Column<double>(type: "double precision", nullable: true),
                    width = table.Column<double>(type: "double precision", nullable: true),
                    height = table.Column<double>(type: "double precision", nullable: true),
                    color_hex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    icon_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_locker_cabinets", x => x.id);
                    table.ForeignKey(
                        name: "fk_locker_cabinets_map_floors_floor_id",
                        column: x => x.floor_id,
                        principalTable: "map_floors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "locker_compartments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cabinet_id = table.Column<long>(type: "bigint", nullable: false),
                    row_index = table.Column<int>(type: "integer", nullable: false),
                    col_index = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_locker_compartments", x => x.id);
                    table.ForeignKey(
                        name: "fk_locker_compartments_locker_cabinets_cabinet_id",
                        column: x => x.cabinet_id,
                        principalTable: "locker_cabinets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_locker_cabinets_floor_id",
                table: "locker_cabinets",
                column: "floor_id");

            migrationBuilder.CreateIndex(
                name: "ix_locker_cabinets_public_id",
                table: "locker_cabinets",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locker_cabinets_tenant_id",
                table: "locker_cabinets",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_locker_cabinets_tenant_id_code",
                table: "locker_cabinets",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_locker_cabinets_tenant_id_floor_id",
                table: "locker_cabinets",
                columns: new[] { "tenant_id", "floor_id" });

            migrationBuilder.CreateIndex(
                name: "ix_locker_compartments_cabinet_id",
                table: "locker_compartments",
                column: "cabinet_id");

            migrationBuilder.CreateIndex(
                name: "ix_locker_compartments_public_id",
                table: "locker_compartments",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_locker_compartments_tenant_id",
                table: "locker_compartments",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_locker_compartments_tenant_id_cabinet_id",
                table: "locker_compartments",
                columns: new[] { "tenant_id", "cabinet_id" });

            migrationBuilder.CreateIndex(
                name: "ix_locker_compartments_tenant_id_code",
                table: "locker_compartments",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.EnableTenantRls("locker_cabinets");
            migrationBuilder.EnableTenantRls("locker_compartments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("locker_compartments");
            migrationBuilder.DisableTenantRls("locker_cabinets");

            migrationBuilder.DropTable(
                name: "locker_compartments");

            migrationBuilder.DropTable(
                name: "locker_cabinets");
        }
    }
}
