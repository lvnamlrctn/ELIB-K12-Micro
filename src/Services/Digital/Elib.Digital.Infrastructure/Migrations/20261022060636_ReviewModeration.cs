using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Digital.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reject_reason",
                table: "ebook_reviews",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rejected_at",
                table: "ebook_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "ebook_reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reviewed_by",
                table: "ebook_reviews",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reject_reason",
                table: "ebook_reviews");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "ebook_reviews");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "ebook_reviews");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                table: "ebook_reviews");
        }
    }
}
