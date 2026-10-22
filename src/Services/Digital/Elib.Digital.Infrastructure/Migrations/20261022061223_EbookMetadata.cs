using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EbookMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "version",
                table: "ebook_items",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "ebook_metadata_values",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    field_id = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_ebook_metadata_values", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "metadata_fields",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    schema_id = table.Column<long>(type: "bigint", nullable: false),
                    field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subfield = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description_vn = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description_en = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    input = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    export_field = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_metadata_fields", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "metadata_schemas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name_space = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
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
                    table.PrimaryKey("pk_metadata_schemas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_metadata_values_public_id",
                table: "ebook_metadata_values",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_metadata_values_tenant_id",
                table: "ebook_metadata_values",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_metadata_values_tenant_id_ebook_item_id_field_id_sort",
                table: "ebook_metadata_values",
                columns: new[] { "tenant_id", "ebook_item_id", "field_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_metadata_values_tenant_id_field_id",
                table: "ebook_metadata_values",
                columns: new[] { "tenant_id", "field_id" });

            migrationBuilder.CreateIndex(
                name: "ix_metadata_fields_public_id",
                table: "metadata_fields",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_metadata_fields_tenant_id",
                table: "metadata_fields",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_metadata_fields_tenant_id_schema_id_field_subfield",
                table: "metadata_fields",
                columns: new[] { "tenant_id", "schema_id", "field", "subfield" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_metadata_schemas_public_id",
                table: "metadata_schemas",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_metadata_schemas_tenant_id",
                table: "metadata_schemas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_metadata_schemas_tenant_id_short_id",
                table: "metadata_schemas",
                columns: new[] { "tenant_id", "short_id" },
                unique: true);

            migrationBuilder.EnableTenantRls("metadata_schemas");
            migrationBuilder.EnableTenantRls("metadata_fields");
            migrationBuilder.EnableTenantRls("ebook_metadata_values");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ebook_metadata_values");

            migrationBuilder.DropTable(
                name: "metadata_fields");

            migrationBuilder.DropTable(
                name: "metadata_schemas");

            migrationBuilder.DropColumn(
                name: "version",
                table: "ebook_items");
        }
    }
}
