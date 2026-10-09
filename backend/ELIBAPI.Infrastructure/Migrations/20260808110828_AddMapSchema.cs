using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMapSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "map");

            migrationBuilder.CreateTable(
                name: "MapBuilding",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_MapBuilding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapEquipment",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ObjectId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    ConditionStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MapEquipment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapFloor",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<long>(type: "bigint", nullable: false),
                    FloorNumber = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LayoutImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<double>(type: "float", nullable: true),
                    Height = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_MapFloor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapFloorUtility",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FloorId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: true),
                    ConditionStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IconName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_MapFloorUtility", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapObject",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FloorId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ObjectType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<int>(type: "int", nullable: true),
                    PositionX = table.Column<double>(type: "float", nullable: true),
                    PositionY = table.Column<double>(type: "float", nullable: true),
                    Width = table.Column<double>(type: "float", nullable: true),
                    Height = table.Column<double>(type: "float", nullable: true),
                    ColorHex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IconName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_MapObject", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapShelfDetail",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ObjectId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryRange = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubjectName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MapShelfDetail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MapShelfRow",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShelfDetailId = table.Column<long>(type: "bigint", nullable: false),
                    RowIndex = table.Column<int>(type: "int", nullable: true),
                    DdcStart = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DdcEnd = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MapShelfRow", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapBuilding",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapEquipment",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapFloor",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapFloorUtility",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapObject",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapShelfDetail",
                schema: "map");

            migrationBuilder.DropTable(
                name: "MapShelfRow",
                schema: "map");
        }
    }
}
