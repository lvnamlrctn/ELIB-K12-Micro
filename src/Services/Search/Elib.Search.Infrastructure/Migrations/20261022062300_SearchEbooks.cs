using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Search.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SearchEbooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ebooks",
                table: "search_sync_states",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "search_ebooks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    ebook_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    author = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    other_title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    publisher = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publish_year = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    keywords = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    collection_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    subject_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    topic_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    type_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    cover_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    free = table.Column<bool>(type: "boolean", nullable: false),
                    has_file = table.Column<bool>(type: "boolean", nullable: false),
                    linked_bib_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    title_fold = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    author_fold = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    search_text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sync_run = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_ebooks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_search_ebooks_tenant_id",
                table: "search_ebooks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_ebooks_tenant_id_ebook_public_id",
                table: "search_ebooks",
                columns: new[] { "tenant_id", "ebook_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_ebooks_tenant_id_status_deleted_year",
                table: "search_ebooks",
                columns: new[] { "tenant_id", "status", "deleted", "year" });


            foreach (var column in new[] { "search_text", "title_fold", "author_fold" })
                migrationBuilder.Sql($"CREATE INDEX ix_search_ebooks_{column}_trgm ON search_ebooks USING gin ({column} gin_trgm_ops);");
            migrationBuilder.EnableTenantRls("search_ebooks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_ebooks");

            migrationBuilder.DropColumn(
                name: "ebooks",
                table: "search_sync_states");
        }
    }
}
