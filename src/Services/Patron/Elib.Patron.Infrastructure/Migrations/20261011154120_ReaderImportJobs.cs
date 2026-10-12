using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Patron.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReaderImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reader_import_jobs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file = table.Column<byte[]>(type: "bytea", nullable: false),
                    options_json = table.Column<string>(type: "text", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    to_create = table.Column<int>(type: "integer", nullable: false),
                    to_update = table.Column<int>(type: "integer", nullable: false),
                    error_count = table.Column<int>(type: "integer", nullable: false),
                    errors_json = table.Column<string>(type: "text", nullable: false),
                    processed = table.Column<int>(type: "integer", nullable: false),
                    created = table.Column<int>(type: "integer", nullable: false),
                    updated = table.Column<int>(type: "integer", nullable: false),
                    failed = table.Column<int>(type: "integer", nullable: false),
                    error_file = table.Column<byte[]>(type: "bytea", nullable: true),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_reader_import_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reader_import_jobs_public_id",
                table: "reader_import_jobs",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reader_import_jobs_tenant_id",
                table: "reader_import_jobs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_reader_import_jobs_tenant_id_status",
                table: "reader_import_jobs",
                columns: new[] { "tenant_id", "status" });

            // Bảng thuộc đơn vị bật RLS (docs 04 §2.1).
            migrationBuilder.EnableTenantRls("reader_import_jobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("reader_import_jobs");
            migrationBuilder.DropTable(
                name: "reader_import_jobs");
        }
    }
}
