using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogsAndPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ebook_collections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    allow_download = table.Column<bool>(type: "boolean", nullable: false),
                    share = table.Column<bool>(type: "boolean", nullable: false),
                    cover_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ebook_collections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_dig_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description_vn = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    description_en = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
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
                    table.PrimaryKey("pk_ebook_dig_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_subjects",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ddc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ebook_subjects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_topics",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    requires_login = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    keyword = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    page_title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ddc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    level = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ebook_topics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "policy_digital_by_collections",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_type_id = table.Column<long>(type: "bigint", nullable: true),
                    collection_id = table.Column<long>(type: "bigint", nullable: false),
                    max_page = table.Column<int>(type: "integer", nullable: true),
                    max_size = table.Column<long>(type: "bigint", nullable: true),
                    max_document = table.Column<int>(type: "integer", nullable: true),
                    read = table.Column<bool>(type: "boolean", nullable: false),
                    comment = table.Column<bool>(type: "boolean", nullable: false),
                    download = table.Column<bool>(type: "boolean", nullable: false),
                    offline_days = table.Column<int>(type: "integer", nullable: true),
                    reservation_expire_hours = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_policy_digital_by_collections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "policy_digitals",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_type_id = table.Column<long>(type: "bigint", nullable: true),
                    max_page = table.Column<int>(type: "integer", nullable: true),
                    max_size = table.Column<long>(type: "bigint", nullable: true),
                    max_document = table.Column<int>(type: "integer", nullable: true),
                    reservation_expire_hours = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_policy_digitals", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_collections_public_id",
                table: "ebook_collections",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_collections_tenant_id",
                table: "ebook_collections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_collections_tenant_id_parent_id",
                table: "ebook_collections",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_dig_types_public_id",
                table: "ebook_dig_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_dig_types_tenant_id",
                table: "ebook_dig_types",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_dig_types_tenant_id_code",
                table: "ebook_dig_types",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_subjects_public_id",
                table: "ebook_subjects",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_subjects_tenant_id",
                table: "ebook_subjects",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_subjects_tenant_id_parent_id",
                table: "ebook_subjects",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_topics_public_id",
                table: "ebook_topics",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_topics_tenant_id",
                table: "ebook_topics",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_topics_tenant_id_parent_id",
                table: "ebook_topics",
                columns: new[] { "tenant_id", "parent_id" });

            migrationBuilder.CreateIndex(
                name: "ix_policy_digital_by_collections_public_id",
                table: "policy_digital_by_collections",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_policy_digital_by_collections_tenant_id",
                table: "policy_digital_by_collections",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_policy_digital_by_collections_tenant_id_collection_id_reade",
                table: "policy_digital_by_collections",
                columns: new[] { "tenant_id", "collection_id", "reader_type_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_policy_digitals_public_id",
                table: "policy_digitals",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_policy_digitals_tenant_id",
                table: "policy_digitals",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_policy_digitals_tenant_id_reader_type_id",
                table: "policy_digitals",
                columns: new[] { "tenant_id", "reader_type_id" },
                unique: true,
                filter: "is_deleted = false");

            // Lớp bảo vệ thứ hai (docs 04 §2.1): mọi bảng của đơn vị bật RLS.
            migrationBuilder.EnableTenantRls("ebook_collections");
            migrationBuilder.EnableTenantRls("ebook_subjects");
            migrationBuilder.EnableTenantRls("ebook_topics");
            migrationBuilder.EnableTenantRls("ebook_dig_types");
            migrationBuilder.EnableTenantRls("policy_digitals");
            migrationBuilder.EnableTenantRls("policy_digital_by_collections");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("policy_digital_by_collections");
            migrationBuilder.DisableTenantRls("policy_digitals");
            migrationBuilder.DisableTenantRls("ebook_dig_types");
            migrationBuilder.DisableTenantRls("ebook_topics");
            migrationBuilder.DisableTenantRls("ebook_subjects");
            migrationBuilder.DisableTenantRls("ebook_collections");

            migrationBuilder.DropTable(
                name: "ebook_collections");

            migrationBuilder.DropTable(
                name: "ebook_dig_types");

            migrationBuilder.DropTable(
                name: "ebook_subjects");

            migrationBuilder.DropTable(
                name: "ebook_topics");

            migrationBuilder.DropTable(
                name: "policy_digital_by_collections");

            migrationBuilder.DropTable(
                name: "policy_digitals");
        }
    }
}
