using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Search.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Z3950Servers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "z3950_servers",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    database_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    record_syntax = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    user_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    password = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sru_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    group_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    show_on_opac = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_z3950_servers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_z3950_servers_public_id",
                table: "z3950_servers",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_z3950_servers_tenant_id",
                table: "z3950_servers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_z3950_servers_tenant_id_name",
                table: "z3950_servers",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.EnableTenantRls("z3950_servers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "z3950_servers");
        }
    }
}
