using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminTaskMonitorRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PayloadPurgedAt",
                schema: "dbo",
                table: "AdminTask",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResultPurgedAt",
                schema: "dbo",
                table: "AdminTask",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RetentionHold",
                schema: "dbo",
                table: "AdminTask",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdminTaskControlEvent",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperatorId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StateBefore = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    StateAfter = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    RequestId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTaskControlEvent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminTaskRetentionRun",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DryRun = table.Column<bool>(type: "bit", nullable: false),
                    TaskCount = table.Column<int>(type: "int", nullable: false),
                    ChunkCount = table.Column<int>(type: "int", nullable: false),
                    EstimatedBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminTaskRetentionRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminTask_State_FinishedAt",
                schema: "dbo",
                table: "AdminTask",
                columns: new[] { "State", "FinishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminTaskControlEvent_RequestId",
                schema: "dbo",
                table: "AdminTaskControlEvent",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminTaskControlEvent_TaskId",
                schema: "dbo",
                table: "AdminTaskControlEvent",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminTaskControlEvent",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AdminTaskRetentionRun",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_AdminTask_State_FinishedAt",
                schema: "dbo",
                table: "AdminTask");

            migrationBuilder.DropColumn(
                name: "PayloadPurgedAt",
                schema: "dbo",
                table: "AdminTask");

            migrationBuilder.DropColumn(
                name: "ResultPurgedAt",
                schema: "dbo",
                table: "AdminTask");

            migrationBuilder.DropColumn(
                name: "RetentionHold",
                schema: "dbo",
                table: "AdminTask");
        }
    }
}
