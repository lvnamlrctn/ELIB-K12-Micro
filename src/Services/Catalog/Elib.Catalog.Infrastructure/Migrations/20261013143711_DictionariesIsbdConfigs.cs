using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Catalog.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DictionariesIsbdConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dictionary_terms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    use_count = table.Column<int>(type: "integer", nullable: false),
                    auto_added = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_dictionary_terms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "isbd_configs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bib_type_id = table.Column<long>(type: "bigint", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    rules = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_isbd_configs", x => x.id);
                    table.ForeignKey(
                        name: "fk_isbd_configs_bib_types_bib_type_id",
                        column: x => x.bib_type_id,
                        principalTable: "bib_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_terms_public_id",
                table: "dictionary_terms",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_terms_tenant_id",
                table: "dictionary_terms",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_dictionary_terms_tenant_id_kind_key",
                table: "dictionary_terms",
                columns: new[] { "tenant_id", "kind", "key" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_isbd_configs_bib_type_id",
                table: "isbd_configs",
                column: "bib_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_isbd_configs_public_id",
                table: "isbd_configs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_isbd_configs_tenant_id",
                table: "isbd_configs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_isbd_configs_tenant_id_bib_type_id",
                table: "isbd_configs",
                columns: new[] { "tenant_id", "bib_type_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.EnableTenantRls("dictionary_terms");
            migrationBuilder.EnableTenantRls("isbd_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dictionary_terms");

            migrationBuilder.DropTable(
                name: "isbd_configs");
        }
    }
}
