using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreReplica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "store_replicas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    store_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_replicas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_store_replicas_tenant_id",
                table: "store_replicas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_store_replicas_tenant_id_store_public_id",
                table: "store_replicas",
                columns: new[] { "tenant_id", "store_public_id" },
                unique: true);

            migrationBuilder.EnableTenantRls("store_replicas");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "store_replicas");
        }
    }
}
