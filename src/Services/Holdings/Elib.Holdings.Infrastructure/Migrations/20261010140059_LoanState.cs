using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Holdings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoanState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "loan_card_no",
                table: "items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "loan_due_at",
                table: "items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "loan_public_id",
                table: "items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "loan_version",
                table: "items",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "loaned_at",
                table: "items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "on_loan",
                table: "items",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "loan_card_no",
                table: "items");

            migrationBuilder.DropColumn(
                name: "loan_due_at",
                table: "items");

            migrationBuilder.DropColumn(
                name: "loan_public_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "loan_version",
                table: "items");

            migrationBuilder.DropColumn(
                name: "loaned_at",
                table: "items");

            migrationBuilder.DropColumn(
                name: "on_loan",
                table: "items");
        }
    }
}
