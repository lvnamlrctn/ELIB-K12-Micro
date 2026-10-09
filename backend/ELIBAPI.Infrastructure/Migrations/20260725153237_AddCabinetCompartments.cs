using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCabinetCompartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Cols",
                schema: "PrintBook",
                table: "Cabinet",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PositionX",
                schema: "PrintBook",
                table: "Cabinet",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PositionY",
                schema: "PrintBook",
                table: "Cabinet",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Rows",
                schema: "PrintBook",
                table: "Cabinet",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CabinetCompartment",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CabinetId = table.Column<long>(type: "bigint", nullable: false),
                    RowIndex = table.Column<int>(type: "int", nullable: false),
                    ColIndex = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_CabinetCompartment", x => x.Id);
                });

            migrationBuilder.Sql(@"
INSERT INTO PrintBook.CabinetCompartment (CabinetId, RowIndex, ColIndex, Code, Name, Status, TenantId, PublicId)
SELECT Id, 0, 0, Code, N'Ngăn 1', Status, TenantId, NEWID() FROM PrintBook.Cabinet WHERE IsDelete != 2 OR IsDelete IS NULL;

UPDATE ko SET ko.Keyid = cc.Id
FROM PrintBook.KeyOut ko JOIN PrintBook.CabinetCompartment cc ON cc.CabinetId = ko.Keyid;

UPDATE ki SET ki.Keyid = cc.Id
FROM PrintBook.KeyIn ki JOIN PrintBook.CabinetCompartment cc ON cc.CabinetId = ki.Keyid;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CabinetCompartment",
                schema: "PrintBook");

            migrationBuilder.DropColumn(
                name: "Cols",
                schema: "PrintBook",
                table: "Cabinet");

            migrationBuilder.DropColumn(
                name: "PositionX",
                schema: "PrintBook",
                table: "Cabinet");

            migrationBuilder.DropColumn(
                name: "PositionY",
                schema: "PrintBook",
                table: "Cabinet");

            migrationBuilder.DropColumn(
                name: "Rows",
                schema: "PrintBook",
                table: "Cabinet");
        }
    }
}
