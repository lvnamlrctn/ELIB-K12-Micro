using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminUiPreferenceAndWorkAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Type",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Record_Type_Code",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "record_type_code");

            migrationBuilder.RenameColumn(
                name: "Name",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Materal_Type",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "materal_type");

            migrationBuilder.RenameColumn(
                name: "Code",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "code");

            migrationBuilder.RenameColumn(
                name: "Bib_Level",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "bib_level");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "id");

            // 2 bảng dưới đây có thể đã được tạo sẵn bằng script tay (backend/docs/add-work-center.sql — Đợt 15,
            // backend/docs/add-admin-ui-preference.sql — Đợt 21) trước khi có migration này. Database.Migrate() tự chạy
            // lúc khởi động trên SQL Server, nên CreateTable thẳng sẽ làm app không lên được ở CSDL đã chạy script
            // -> tạo có điều kiện. Kiểu cột giữ đúng như EF scaffold (khớp snapshot).
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminUiPreference')
BEGIN
    CREATE TABLE [dbo].[AdminUiPreference] (
        [ActorId]      bigint        NOT NULL,
        [PageKey]      nvarchar(40)  NOT NULL,
        [SettingsJson] nvarchar(max) NOT NULL,
        [Revision]     int           NOT NULL,
        [UpdatedAt]    datetime2     NOT NULL,
        [TenantId]     bigint        NULL,
        CONSTRAINT [PK_AdminUiPreference] PRIMARY KEY ([ActorId], [PageKey])
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('dbo') AND name = 'AdminWorkAssignment')
BEGIN
    CREATE TABLE [dbo].[AdminWorkAssignment] (
        [SourceType]     nvarchar(450)    NOT NULL,
        [SourcePublicId] uniqueidentifier NOT NULL,
        [AssigneeId]     bigint           NULL,
        [DueAtUtc]       datetime2        NULL,
        [Version]        int              NOT NULL,
        [UpdatedBy]      bigint           NOT NULL,
        [UpdatedAtUtc]   datetime2        NOT NULL,
        [TenantId]       bigint           NULL,
        CONSTRAINT [PK_AdminWorkAssignment] PRIMARY KEY ([SourceType], [SourcePublicId])
    );
    CREATE INDEX [IX_AdminWorkAssignment_SourceType_AssigneeId_DueAtUtc]
        ON [dbo].[AdminWorkAssignment] ([SourceType], [AssigneeId], [DueAtUtc]);
    CREATE INDEX [IX_AdminWorkAssignment_TenantId] ON [dbo].[AdminWorkAssignment] ([TenantId]);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminUiPreference",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "AdminWorkAssignment",
                schema: "dbo");

            migrationBuilder.RenameColumn(
                name: "type",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "record_type_code",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "Record_Type_Code");

            migrationBuilder.RenameColumn(
                name: "name",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "materal_type",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "Materal_Type");

            migrationBuilder.RenameColumn(
                name: "code",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "Code");

            migrationBuilder.RenameColumn(
                name: "bib_level",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "Bib_Level");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "PrintBook",
                table: "Bib_Type",
                newName: "Id");
        }
    }
}
