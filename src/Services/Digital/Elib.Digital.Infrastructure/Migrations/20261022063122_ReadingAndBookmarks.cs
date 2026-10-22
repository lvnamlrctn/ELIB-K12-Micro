using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReadingAndBookmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ebook_access_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_ebook_access_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_bookmarks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("pk_ebook_bookmarks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_read_progress",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_page = table.Column<int>(type: "integer", nullable: false),
                    max_page = table.Column<int>(type: "integer", nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    last_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_ebook_read_progress", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_read_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    ebook_file_id = table.Column<long>(type: "bigint", nullable: true),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_key = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_page = table.Column<int>(type: "integer", nullable: false),
                    max_page = table.Column<int>(type: "integer", nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    active_seconds = table.Column<int>(type: "integer", nullable: false),
                    device = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("pk_ebook_read_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_access_logs_public_id",
                table: "ebook_access_logs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_access_logs_tenant_id",
                table: "ebook_access_logs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_access_logs_tenant_id_at",
                table: "ebook_access_logs",
                columns: new[] { "tenant_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_bookmarks_public_id",
                table: "ebook_bookmarks",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_bookmarks_tenant_id",
                table: "ebook_bookmarks",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_bookmarks_tenant_id_reader_public_id_ebook_item_id_pa",
                table: "ebook_bookmarks",
                columns: new[] { "tenant_id", "reader_public_id", "ebook_item_id", "page" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_progress_public_id",
                table: "ebook_read_progress",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_progress_tenant_id",
                table: "ebook_read_progress",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_progress_tenant_id_reader_public_id_ebook_item_id",
                table: "ebook_read_progress",
                columns: new[] { "tenant_id", "reader_public_id", "ebook_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_sessions_public_id",
                table: "ebook_read_sessions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_sessions_tenant_id",
                table: "ebook_read_sessions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_sessions_tenant_id_ebook_item_id_started_at",
                table: "ebook_read_sessions",
                columns: new[] { "tenant_id", "ebook_item_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_sessions_tenant_id_reader_public_id_session_key",
                table: "ebook_read_sessions",
                columns: new[] { "tenant_id", "reader_public_id", "session_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_read_sessions_tenant_id_started_at",
                table: "ebook_read_sessions",
                columns: new[] { "tenant_id", "started_at" });

            foreach (var table in new[] { "ebook_read_sessions", "ebook_read_progress", "ebook_access_logs", "ebook_bookmarks" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ebook_access_logs");

            migrationBuilder.DropTable(
                name: "ebook_bookmarks");

            migrationBuilder.DropTable(
                name: "ebook_read_progress");

            migrationBuilder.DropTable(
                name: "ebook_read_sessions");
        }
    }
}
