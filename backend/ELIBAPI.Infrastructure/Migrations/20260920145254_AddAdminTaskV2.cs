using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminTaskV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminTask",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Preview = table.Column<bool>(type: "bit", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TotalChunks = table.Column<int>(type: "int", nullable: false),
                    CompletedChunks = table.Column<int>(type: "int", nullable: false),
                    TotalItems = table.Column<int>(type: "int", nullable: false),
                    CompletedItems = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveFailures = table.Column<int>(type: "int", nullable: false),
                    StopRequested = table.Column<bool>(type: "bit", nullable: false),
                    SourcePreviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewToken = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTask", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminTaskChunk",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Result = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Completed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTaskChunk", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminWorkerHeartbeat",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastSeen = table.Column<DateTime>(type: "datetime2", nullable: false),
                    KeyFingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminWorkerHeartbeat", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminTask_ActorId",
                schema: "dbo",
                table: "AdminTask",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminTask_ReviewToken",
                schema: "dbo",
                table: "AdminTask",
                column: "ReviewToken");

            migrationBuilder.CreateIndex(
                name: "IX_AdminTask_State",
                schema: "dbo",
                table: "AdminTask",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_AdminTaskChunk_TaskId_Position",
                schema: "dbo",
                table: "AdminTaskChunk",
                columns: new[] { "TaskId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminTask",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AdminTaskChunk",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AdminWorkerHeartbeat",
                schema: "dbo");
        }
    }
}
