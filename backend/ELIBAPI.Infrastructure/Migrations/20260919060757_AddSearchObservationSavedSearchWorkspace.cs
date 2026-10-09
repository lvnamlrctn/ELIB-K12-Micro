using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchObservationSavedSearchWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReaderSavedSearch",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    RequestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AlertsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    KnownIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasUnread = table.Column<bool>(type: "bit", nullable: false),
                    LastCheckedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Total = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderSavedSearch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderWorkspace",
                schema: "dbo",
                columns: table => new
                {
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderWorkspace", x => x.ReaderId);
                });

            migrationBuilder.CreateTable(
                name: "SearchObservation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DocType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Total = table.Column<long>(type: "bigint", nullable: false),
                    ElapsedMs = table.Column<long>(type: "bigint", nullable: false),
                    Failed = table.Column<bool>(type: "bit", nullable: false),
                    UsedFallback = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SearchObservation", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReaderSavedSearch",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReaderWorkspace",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "SearchObservation",
                schema: "dbo");
        }
    }
}
