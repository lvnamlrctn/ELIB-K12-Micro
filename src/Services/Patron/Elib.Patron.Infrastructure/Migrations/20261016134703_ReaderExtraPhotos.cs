using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Patron.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReaderExtraPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reader_photos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_id = table.Column<long>(type: "bigint", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reader_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_reader_photos_readers_reader_id",
                        column: x => x.reader_id,
                        principalTable: "readers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reader_photos_reader_id",
                table: "reader_photos",
                column: "reader_id");

            migrationBuilder.CreateIndex(
                name: "ix_reader_photos_tenant_id",
                table: "reader_photos",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_reader_photos_tenant_id_reader_id_file_id",
                table: "reader_photos",
                columns: new[] { "tenant_id", "reader_id", "file_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reader_photos");
        }
    }
}
