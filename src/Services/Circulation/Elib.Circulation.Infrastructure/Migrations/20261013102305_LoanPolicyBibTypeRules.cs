using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoanPolicyBibTypeRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bib_type_rules",
                table: "loan_policies",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shipping_fee",
                table: "loan_policies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tech_fee",
                table: "loan_policies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "bib_type_id",
                table: "bib_snapshots",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "type_known",
                table: "bib_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bib_type_rules",
                table: "loan_policies");

            migrationBuilder.DropColumn(
                name: "shipping_fee",
                table: "loan_policies");

            migrationBuilder.DropColumn(
                name: "tech_fee",
                table: "loan_policies");

            migrationBuilder.DropColumn(
                name: "bib_type_id",
                table: "bib_snapshots");

            migrationBuilder.DropColumn(
                name: "type_known",
                table: "bib_snapshots");
        }
    }
}
