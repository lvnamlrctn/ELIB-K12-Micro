using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Patron.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReaderGroupMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reader_group_members",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_id = table.Column<long>(type: "bigint", nullable: false),
                    group_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reader_group_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_reader_group_members_reader_groups_group_id",
                        column: x => x.group_id,
                        principalTable: "reader_groups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reader_group_members_readers_reader_id",
                        column: x => x.reader_id,
                        principalTable: "readers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reader_group_members_group_id",
                table: "reader_group_members",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_reader_group_members_reader_id",
                table: "reader_group_members",
                column: "reader_id");

            migrationBuilder.CreateIndex(
                name: "ix_reader_group_members_tenant_id",
                table: "reader_group_members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_reader_group_members_tenant_id_group_id",
                table: "reader_group_members",
                columns: new[] { "tenant_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reader_group_members_tenant_id_reader_id_group_id",
                table: "reader_group_members",
                columns: new[] { "tenant_id", "reader_id", "group_id" },
                unique: true);

            // Bảng thuộc đơn vị bật RLS (docs 04 §2.1).
            migrationBuilder.EnableTenantRls("reader_group_members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableTenantRls("reader_group_members");
            migrationBuilder.DropTable(
                name: "reader_group_members");
        }
    }
}
