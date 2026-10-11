using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Elib.BuildingBlocks.Persistence;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Search.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CoverAndQueryStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_url",
                table: "search_bibs",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "search_queries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    query_id = table.Column<Guid>(type: "uuid", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text_fold = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    advanced = table.Column<bool>(type: "boolean", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    clicks = table.Column<int>(type: "integer", nullable: false),
                    first_click_position = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_queries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_search_queries_tenant_id",
                table: "search_queries",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_queries_tenant_id_at",
                table: "search_queries",
                columns: new[] { "tenant_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_search_queries_tenant_id_query_id",
                table: "search_queries",
                columns: new[] { "tenant_id", "query_id" },
                unique: true);

            migrationBuilder.EnableTenantRls("search_queries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "search_queries");

            migrationBuilder.DropColumn(
                name: "cover_url",
                table: "search_bibs");
        }
    }
}
