using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sip2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sip2_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    station_id = table.Column<long>(type: "bigint", nullable: false),
                    command = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    request = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    response = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ok = table.Column<bool>(type: "boolean", nullable: false),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: false),
                    remote_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sip2_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sip2_stations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    login_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    login_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    circ_place_id = table.Column<long>(type: "bigint", nullable: false),
                    require_patron_password = table.Column<bool>(type: "boolean", nullable: false),
                    ascii_only = table.Column<bool>(type: "boolean", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_sip2_stations", x => x.id);
                    table.ForeignKey(
                        name: "fk_sip2_stations_circ_places_circ_place_id",
                        column: x => x.circ_place_id,
                        principalTable: "circ_places",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sip2_logs_tenant_id",
                table: "sip2_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sip2_logs_tenant_id_at",
                table: "sip2_logs",
                columns: new[] { "tenant_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_sip2_logs_tenant_id_station_id_at",
                table: "sip2_logs",
                columns: new[] { "tenant_id", "station_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_sip2_stations_circ_place_id",
                table: "sip2_stations",
                column: "circ_place_id");

            migrationBuilder.CreateIndex(
                name: "ix_sip2_stations_login_key",
                table: "sip2_stations",
                column: "login_key",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_sip2_stations_public_id",
                table: "sip2_stations",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sip2_stations_tenant_id",
                table: "sip2_stations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_sip2_stations_tenant_id_code",
                table: "sip2_stations",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.EnableTenantRls("sip2_stations");
            migrationBuilder.EnableTenantRls("sip2_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sip2_logs");

            migrationBuilder.DropTable(
                name: "sip2_stations");
        }
    }
}
