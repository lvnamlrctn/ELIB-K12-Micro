using Elib.BuildingBlocks.Persistence;
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReaderAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reader_accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    card_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    email = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    card_no_password = table.Column<bool>(type: "boolean", nullable: false),
                    credentials_access_failed_count = table.Column<int>(type: "integer", nullable: false),
                    credentials_last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    credentials_lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    credentials_must_change_password = table.Column<bool>(type: "boolean", nullable: false),
                    credentials_password_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reader_accounts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reader_accounts_tenant_id",
                table: "reader_accounts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_reader_accounts_tenant_id_card_key",
                table: "reader_accounts",
                columns: new[] { "tenant_id", "card_key" });

            migrationBuilder.CreateIndex(
                name: "ix_reader_accounts_tenant_id_reader_public_id",
                table: "reader_accounts",
                columns: new[] { "tenant_id", "reader_public_id" },
                unique: true);

            // Bảng thuộc đơn vị bật RLS (docs 04 §2.1).
            migrationBuilder.EnableTenantRls("reader_accounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("reader_accounts");
            migrationBuilder.DropTable(
                name: "reader_accounts");
        }
    }
}
