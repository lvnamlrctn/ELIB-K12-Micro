using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SharedEbooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hidden_by_tenant",
                table: "ebook_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_replica",
                table: "ebook_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "source_public_id",
                table: "ebook_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "source_tenant_id",
                table: "ebook_items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_public_id",
                table: "ebook_files",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "source_tenant_id",
                table: "ebook_files",
                type: "bigint",
                nullable: true);

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

            migrationBuilder.EnableTenantRls("shared_sync_states");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_tenant_id_source_public_id",
                table: "ebook_items",
                columns: new[] { "tenant_id", "source_public_id" },
                unique: true,
                filter: "source_public_id IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_files_tenant_id_source_public_id",
                table: "ebook_files",
                columns: new[] { "tenant_id", "source_public_id" },
                unique: true,
                filter: "source_public_id IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_shared_sync_states_tenant_id",
                table: "shared_sync_states",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_shared_sync_states_tenant_id_dataset",
                table: "shared_sync_states",
                columns: new[] { "tenant_id", "dataset" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("shared_sync_states");

            migrationBuilder.DropTable(
                name: "shared_sync_states");

            migrationBuilder.DropIndex(
                name: "ix_ebook_items_tenant_id_source_public_id",
                table: "ebook_items");

            migrationBuilder.DropIndex(
                name: "ix_ebook_files_tenant_id_source_public_id",
                table: "ebook_files");

            migrationBuilder.DropColumn(
                name: "hidden_by_tenant",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "is_replica",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "source_public_id",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "source_tenant_id",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "source_public_id",
                table: "ebook_files");

            migrationBuilder.DropColumn(
                name: "source_tenant_id",
                table: "ebook_files");
        }
    }
}
