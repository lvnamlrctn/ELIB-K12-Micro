using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewsAndFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ebook_favorites",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_ebook_favorites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ebook_reviews",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ebook_item_id = table.Column<long>(type: "bigint", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("pk_ebook_reviews", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_favorites_public_id",
                table: "ebook_favorites",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_favorites_tenant_id",
                table: "ebook_favorites",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_favorites_tenant_id_ebook_item_id_reader_public_id",
                table: "ebook_favorites",
                columns: new[] { "tenant_id", "ebook_item_id", "reader_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_favorites_tenant_id_reader_public_id",
                table: "ebook_favorites",
                columns: new[] { "tenant_id", "reader_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_ebook_reviews_public_id",
                table: "ebook_reviews",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_reviews_tenant_id",
                table: "ebook_reviews",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_ebook_reviews_tenant_id_ebook_item_id_status",
                table: "ebook_reviews",
                columns: new[] { "tenant_id", "ebook_item_id", "status" });

            migrationBuilder.EnableTenantRls("ebook_favorites");
            migrationBuilder.EnableTenantRls("ebook_reviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ebook_favorites");

            migrationBuilder.DropTable(
                name: "ebook_reviews");
        }
    }
}
