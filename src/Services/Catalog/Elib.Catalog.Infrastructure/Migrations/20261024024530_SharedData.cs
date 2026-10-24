using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SharedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_replica",
                table: "isbd_configs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_replica",
                table: "dictionary_terms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "shared_sync_states",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    dataset = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    watermark = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shared_sync_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_shared_sync_states_tenant_id",
                table: "shared_sync_states",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_sync_states_tenant_id_dataset",
                table: "shared_sync_states",
                columns: new[] { "tenant_id", "dataset" },
                unique: true);

            migrationBuilder.EnableTenantRls("shared_sync_states");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_sync_states");

            migrationBuilder.DropColumn(
                name: "is_replica",
                table: "isbd_configs");

            migrationBuilder.DropColumn(
                name: "is_replica",
                table: "dictionary_terms");
        }
    }
}
