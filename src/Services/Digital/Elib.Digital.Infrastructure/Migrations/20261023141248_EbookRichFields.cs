using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EbookRichFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "abstract",
                table: "ebook_items",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "advisors",
                table: "ebook_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "authors",
                table: "ebook_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "ebook_items",
                type: "character varying(20000)",
                maxLength: 20000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identifiers",
                table: "ebook_items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "journal_name",
                table: "ebook_items",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "publish_day",
                table: "ebook_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "publish_month",
                table: "ebook_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "publish_year",
                table: "ebook_items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ebook_items_tenant_id_publish_year",
                table: "ebook_items",
                columns: new[] { "tenant_id", "publish_year" });

            // Tài liệu có sẵn chỉ có các chuỗi phẳng: dựng danh sách tác giả/người hướng dẫn từ chuỗi (cả tên vào "Last", không đoán họ/tên) và
            // năm xuất bản số từ chuỗi ngày — nếu không, mở tài liệu cũ rồi lưu sẽ xoá tác giả và bộ lọc năm bỏ sót toàn bộ tài liệu cũ.
            // Bảng có FORCE RLS → bật ngữ cảnh hệ thống trong transaction của migration.
            migrationBuilder.Sql("""
                SELECT set_config('app.tenant_system', 'on', true);
                UPDATE ebook_items SET
                  authors = COALESCE((SELECT jsonb_agg(jsonb_build_object('Last', left(btrim(x), 200))) FROM unnest(string_to_array(author, ';')) AS x WHERE btrim(x) <> ''), '[]'::jsonb),
                  advisors = COALESCE((SELECT jsonb_agg(jsonb_build_object('Last', left(btrim(x), 200))) FROM unnest(string_to_array(old_author, ';')) AS x WHERE btrim(x) <> ''), '[]'::jsonb),
                  identifiers = '[]'::jsonb,
                  publish_year = (regexp_match(publish_date, '(?<![0-9])([12][0-9]{3})(?![0-9])'))[1]::int;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ebook_items_tenant_id_publish_year",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "abstract",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "advisors",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "authors",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "description",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "identifiers",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "journal_name",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "publish_day",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "publish_month",
                table: "ebook_items");

            migrationBuilder.DropColumn(
                name: "publish_year",
                table: "ebook_items");
        }
    }
}
