using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItemsAndFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_file_id",
                table: "ebook_collections");

            migrationBuilder.AddColumn<string>(
                name: "cover_url",
                table: "ebook_collections",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ebook_files",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    media_file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_convert = table.Column<bool>(type: "boolean", nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    file_size = table.Column<double>(type: "double precision", nullable: true),
                    file_ext = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    format_id = table.Column<int>(type: "integer", nullable: true),
                    check_sum_algorithm = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
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
                    table.PrimaryKey("pk_ebook_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<int>(type: "integer", nullable: false),
                    collection_id = table.Column<long>(type: "bigint", nullable: true),
                    subject_id = table.Column<long>(type: "bigint", nullable: true),
                    topic_id = table.Column<long>(type: "bigint", nullable: true),
                    type_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    author = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publisher = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publish_date = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    keyword = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    other_title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    page = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    old_author = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true),
                    language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    free = table.Column<bool>(type: "boolean", nullable: false),
                    allow_download = table.Column<bool>(type: "boolean", nullable: false),
                    print_copies = table.Column<int>(type: "integer", nullable: true),
                    offline_days = table.Column<int>(type: "integer", nullable: true),
                    show = table.Column<int>(type: "integer", nullable: true),
                    index_content = table.Column<int>(type: "integer", nullable: true),
                    share = table.Column<int>(type: "integer", nullable: true),
                    file_version = table.Column<int>(type: "integer", nullable: true),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    total_view = table.Column<int>(type: "integer", nullable: false),
                    total_download = table.Column<int>(type: "integer", nullable: false),
                    submited = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_update = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cover_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    linked_bib_public_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_ebook_items", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_files_public_id",
                table: "ebook_files",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_files_tenant_id",
                table: "ebook_files",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_files_tenant_id_ebook_item_id",
                table: "ebook_files",
                columns: new[] { "tenant_id", "ebook_item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_public_id",
                table: "ebook_items",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_tenant_id",
                table: "ebook_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_tenant_id_collection_id",
                table: "ebook_items",
                columns: new[] { "tenant_id", "collection_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_tenant_id_linked_bib_public_id",
                table: "ebook_items",
                columns: new[] { "tenant_id", "linked_bib_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_tenant_id_subject_id",
                table: "ebook_items",
                columns: new[] { "tenant_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_tenant_id_topic_id",
                table: "ebook_items",
                columns: new[] { "tenant_id", "topic_id" });

            // Lớp bảo vệ thứ hai (docs 04 §2.1): mọi bảng của đơn vị bật RLS.
            migrationBuilder.EnableTenantRls("ebook_items");
            migrationBuilder.EnableTenantRls("ebook_files");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("ebook_files");
            migrationBuilder.DisableTenantRls("ebook_items");

            migrationBuilder.DropTable(
                name: "ebook_files");

            migrationBuilder.DropTable(
                name: "ebook_items");

            migrationBuilder.DropColumn(
                name: "cover_url",
                table: "ebook_collections");

            migrationBuilder.AddColumn<Guid>(
                name: "cover_file_id",
                table: "ebook_collections",
                type: "uuid",
                nullable: true);
        }
    }
}
