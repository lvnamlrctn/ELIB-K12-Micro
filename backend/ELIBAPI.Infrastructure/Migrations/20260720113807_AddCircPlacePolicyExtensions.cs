using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCircPlacePolicyExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AllowOpacRequest",
                schema: "PrintBook",
                table: "PolicyCirc",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfBookAccept",
                schema: "PrintBook",
                table: "PolicyCirc",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfRenewDays",
                schema: "PrintBook",
                table: "PolicyCirc",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfRenewQty",
                schema: "PrintBook",
                table: "PolicyCirc",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfRequestCount",
                schema: "PrintBook",
                table: "PolicyCirc",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfRequestDays",
                schema: "PrintBook",
                table: "PolicyCirc",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AutoAccept",
                schema: "PrintBook",
                table: "CircPlace",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                schema: "PrintBook",
                table: "CircPlace",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CircPlaceReaderType",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CircPlaceId = table.Column<int>(type: "int", nullable: false),
                    ReaderTypeId = table.Column<long>(type: "bigint", nullable: false),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircPlaceReaderType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CircPlaceStore",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CircPlaceId = table.Column<int>(type: "int", nullable: false),
                    StoreId = table.Column<long>(type: "bigint", nullable: false),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircPlaceStore", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocGroup",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyCircDocGroup",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PolicyCircId = table.Column<int>(type: "int", nullable: false),
                    DocGroupId = table.Column<int>(type: "int", nullable: false),
                    NumberOfBook = table.Column<int>(type: "int", nullable: true),
                    NumberOfRequest = table.Column<int>(type: "int", nullable: true),
                    NumberOfRenewQty = table.Column<int>(type: "int", nullable: true),
                    NumberOfBookAccept = table.Column<int>(type: "int", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyCircDocGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyCircFine",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PolicyCircId = table.Column<int>(type: "int", nullable: false),
                    FineTypeId = table.Column<long>(type: "bigint", nullable: true),
                    FineMethodId = table.Column<int>(type: "int", nullable: true),
                    HoldCardDays = table.Column<int>(type: "int", nullable: true),
                    FineAmount = table.Column<double>(type: "float", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PolicyCircFine", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CircPlaceReaderType",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "CircPlaceStore",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DocGroup",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "PolicyCircDocGroup",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "PolicyCircFine",
                schema: "PrintBook");

            migrationBuilder.DropColumn(
                name: "AllowOpacRequest",
                schema: "PrintBook",
                table: "PolicyCirc");

            migrationBuilder.DropColumn(
                name: "NumberOfBookAccept",
                schema: "PrintBook",
                table: "PolicyCirc");

            migrationBuilder.DropColumn(
                name: "NumberOfRenewDays",
                schema: "PrintBook",
                table: "PolicyCirc");

            migrationBuilder.DropColumn(
                name: "NumberOfRenewQty",
                schema: "PrintBook",
                table: "PolicyCirc");

            migrationBuilder.DropColumn(
                name: "NumberOfRequestCount",
                schema: "PrintBook",
                table: "PolicyCirc");

            migrationBuilder.DropColumn(
                name: "NumberOfRequestDays",
                schema: "PrintBook",
                table: "PolicyCirc");

            migrationBuilder.DropColumn(
                name: "AutoAccept",
                schema: "PrintBook",
                table: "CircPlace");

            migrationBuilder.DropColumn(
                name: "Code",
                schema: "PrintBook",
                table: "CircPlace");
        }
    }
}
