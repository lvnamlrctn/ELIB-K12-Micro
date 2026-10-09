using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "PrintBook");

            migrationBuilder.EnsureSchema(
                name: "cms");

            migrationBuilder.EnsureSchema(
                name: "EOffice");

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.EnsureSchema(
                name: "Ebook");

            migrationBuilder.EnsureSchema(
                name: "Evaluate");

            migrationBuilder.CreateTable(
                name: "aacr2_field",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Config_Id = table.Column<int>(type: "int", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Starttp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stoptp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fieldindex = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_aacr2_field", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "aacr2_subfield",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Config_Id = table.Column<int>(type: "int", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subfield = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Starttp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stoptp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nexttp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subfieldindex = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_aacr2_subfield", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_deliverer",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DelivererName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DelivererAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DelivererDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserIddeliverer = table.Column<int>(type: "int", nullable: true),
                    UserIdReceipt = table.Column<int>(type: "int", nullable: true),
                    Store_Id = table.Column<int>(type: "int", nullable: true),
                    Receipt_Id = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sign = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ab_deliverer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_deliverer_detail",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Deliverer_Id = table.Column<long>(type: "bigint", nullable: true),
                    BarcodeId = table.Column<long>(type: "bigint", nullable: true),
                    Store_Id = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ab_deliverer_detail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_deliverer_status",
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
                    table.PrimaryKey("PK_ab_deliverer_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_move",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<long>(type: "bigint", nullable: true),
                    ReceiptName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiptAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DelivererName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DelivererAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DelivererDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserIddeliverer = table.Column<int>(type: "int", nullable: true),
                    UserIdReceipt = table.Column<int>(type: "int", nullable: true),
                    StoreDeliver_Id = table.Column<int>(type: "int", nullable: true),
                    StoreReceipt_Id = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ab_move", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_move_detail",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Move_Id = table.Column<long>(type: "bigint", nullable: true),
                    BarcodeId = table.Column<long>(type: "bigint", nullable: true),
                    Store_Id = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ab_move_detail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_order",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date_Order = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duedate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source_Id = table.Column<long>(type: "bigint", nullable: true),
                    Supplier_Id = table.Column<int>(type: "int", nullable: true),
                    BUDGET_ID = table.Column<int>(type: "int", nullable: true),
                    CREATED_BY = table.Column<long>(type: "bigint", nullable: true),
                    Order_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    FundId = table.Column<long>(type: "bigint", nullable: true),
                    Payment_status = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ab_order", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_order_detail",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Order_Id = table.Column<long>(type: "bigint", nullable: true),
                    Bibid = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<double>(type: "float", nullable: true),
                    CURRENCY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rate = table.Column<double>(type: "float", nullable: true),
                    Cancel_Reson = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ab_order_detail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_receipt",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Receipt_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source_Id = table.Column<long>(type: "bigint", nullable: true),
                    Supplier_Id = table.Column<long>(type: "bigint", nullable: true),
                    BUDGET_ID = table.Column<long>(type: "bigint", nullable: true),
                    CREATED_BY = table.Column<long>(type: "bigint", nullable: true),
                    Receipt_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<long>(type: "bigint", nullable: true),
                    Payment_Status = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Store_Id = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    FundId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ab_receipt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_receipt_detail",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Receipt_Id = table.Column<long>(type: "bigint", nullable: true),
                    Bibid = table.Column<long>(type: "bigint", nullable: true),
                    Amount = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<double>(type: "float", nullable: true),
                    CURRENCY = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rate = table.Column<double>(type: "float", nullable: true),
                    Order_Id = table.Column<long>(type: "bigint", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ab_receipt_detail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ab_source",
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
                    table.PrimaryKey("PK_ab_source", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADS",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Image = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    AdsGroupId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ADS", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ADSGroup",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ADSGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Agency",
                schema: "EOffice",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Agency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ah_receipt",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Receipt_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source_Id = table.Column<long>(type: "bigint", nullable: true),
                    Supplier_Id = table.Column<long>(type: "bigint", nullable: true),
                    BUDGET_ID = table.Column<long>(type: "bigint", nullable: true),
                    CREATED_BY = table.Column<long>(type: "bigint", nullable: true),
                    Receipt_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<long>(type: "bigint", nullable: true),
                    Payment_Status = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Store_Id = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethodId = table.Column<int>(type: "int", nullable: true),
                    FundId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ah_receipt", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttachFile",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<double>(type: "float", nullable: true),
                    NewsId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AttachFile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Banner",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Banner", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Barcode",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BarcodeNumber_str = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BarcodeNumber = table.Column<int>(type: "int", nullable: true),
                    Store = table.Column<int>(type: "int", nullable: true),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
                    Receipt_Id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Barcode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Barcode_Status",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CommentStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Barcode_Status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bib",
                schema: "PrintBook",
                columns: table => new
                {
                    Bibid = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mfn = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bib_worksheet_id = table.Column<long>(type: "bigint", nullable: true),
                    Bib_type_id = table.Column<long>(type: "bigint", nullable: true),
                    MARC_STATUS = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EbookId = table.Column<long>(type: "bigint", nullable: true),
                    DocNum = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Bib", x => x.Bibid);
                });

            migrationBuilder.CreateTable(
                name: "Bib_Type",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bib_Level = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Record_Type_Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Materal_Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Bib_Type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bib_worksheet",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Usmarc = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bib_Type_Id = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_bib_worksheet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BibData",
                schema: "PrintBook",
                columns: table => new
                {
                    BibDataId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fk = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    L1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    L2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataUnsign = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_BibData", x => x.BibDataId);
                });

            migrationBuilder.CreateTable(
                name: "BibDataOrder",
                schema: "PrintBook",
                columns: table => new
                {
                    BibDataId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fk = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    L1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    L2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataUnsign = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_BibDataOrder", x => x.BibDataId);
                });

            migrationBuilder.CreateTable(
                name: "BibOrder",
                schema: "PrintBook",
                columns: table => new
                {
                    Bibid = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mfn = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdateTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bib_Worksheet_Id = table.Column<long>(type: "bigint", nullable: true),
                    Bib_Type_Id = table.Column<long>(type: "bigint", nullable: true),
                    MARC_STATUS = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_BibOrder", x => x.Bibid);
                });

            migrationBuilder.CreateTable(
                name: "BibXML",
                schema: "PrintBook",
                columns: table => new
                {
                    BibId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Page = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bib_Worksheet_Id = table.Column<int>(type: "int", nullable: true),
                    Bib_Type_Id = table.Column<int>(type: "int", nullable: true),
                    Isbd = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Accr2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    DDC = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_BibXML", x => x.BibId);
                });

            migrationBuilder.CreateTable(
                name: "BibXMLOrder",
                schema: "PrintBook",
                columns: table => new
                {
                    BibId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Page = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bib_Worksheet_Id = table.Column<int>(type: "int", nullable: true),
                    Bib_Type_Id = table.Column<int>(type: "int", nullable: true),
                    Isbd = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Accr2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    DDC = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_BibXMLOrder", x => x.BibId);
                });

            migrationBuilder.CreateTable(
                name: "Book_Group",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bib_Id1 = table.Column<long>(type: "bigint", nullable: true),
                    Bib_Id2 = table.Column<long>(type: "bigint", nullable: true),
                    Link = table.Column<int>(type: "int", nullable: true),
                    Mode = table.Column<int>(type: "int", nullable: true),
                    Fieldid = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Book_Group", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "book_group_detail",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Book_Group_Id = table.Column<long>(type: "bigint", nullable: true),
                    Bibid = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_book_group_detail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookIn",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BorrowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BookOutId = table.Column<long>(type: "bigint", nullable: true),
                    Renew = table.Column<int>(type: "int", nullable: true),
                    CircPlace = table.Column<long>(type: "bigint", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    FineValue = table.Column<double>(type: "float", nullable: true),
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
                    table.PrimaryKey("PK_BookIn", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookOut",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    BorrowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Renew = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CircPlace = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reg_Seq_Id = table.Column<long>(type: "bigint", nullable: true),
                    Store = table.Column<long>(type: "bigint", nullable: true),
                    Update_By = table.Column<long>(type: "bigint", nullable: true),
                    FineValue = table.Column<double>(type: "float", nullable: true),
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
                    table.PrimaryKey("PK_BookOut", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookRequest",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    BorrowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CircPlace = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    FineValue = table.Column<double>(type: "float", nullable: true),
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
                    table.PrimaryKey("PK_BookRequest", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Budget",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Blance = table.Column<double>(type: "float", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Budget", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "C_Fine",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    FineDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Fine_type_id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Returndate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<double>(type: "float", nullable: true),
                    Fine_method_id = table.Column<int>(type: "int", nullable: true),
                    Borrow_Id = table.Column<long>(type: "bigint", nullable: true),
                    BorrowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Created_by = table.Column<int>(type: "int", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Lanphat = table.Column<int>(type: "int", nullable: true),
                    Bibid = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_C_Fine", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "c_fine_method",
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
                    table.PrimaryKey("PK_c_fine_method", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "C_Fine_type",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status_Reg_Id = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_C_Fine_type", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "C_photo",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reader_Id = table.Column<long>(type: "bigint", nullable: true),
                    Frompage = table.Column<int>(type: "int", nullable: true),
                    ToPage = table.Column<int>(type: "int", nullable: true),
                    Price = table.Column<double>(type: "float", nullable: true),
                    PhotoDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Copy = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_C_photo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "c_queue_status",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_c_queue_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "C_renew",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Borrow_id = table.Column<long>(type: "bigint", nullable: true),
                    Reg_Seq_Id = table.Column<long>(type: "bigint", nullable: true),
                    Reg_Id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Circ_Place_Id = table.Column<int>(type: "int", nullable: true),
                    Status_Id = table.Column<int>(type: "int", nullable: true),
                    Renew_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duedate_Request = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Active = table.Column<int>(type: "int", nullable: true),
                    Status_Email = table.Column<int>(type: "int", nullable: true),
                    Renew_Date_Num = table.Column<int>(type: "int", nullable: true),
                    Borrow_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Duedate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reader_Id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_C_renew", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "C_renew_data",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Borrow_id = table.Column<long>(type: "bigint", nullable: true),
                    Reg_Seq_Id = table.Column<long>(type: "bigint", nullable: true),
                    Reg_Id = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Renew_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Due_Date_Old = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Due_Date_New = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Borrow_Date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reader_Id = table.Column<long>(type: "bigint", nullable: true),
                    Created_By = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_C_renew_data", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cabinet",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CircPlaceId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Cabinet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Category",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLogin = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PageTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Category", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckIn",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Readerid = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_CheckIn", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckOut",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    CheckInTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    CheckInId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_CheckOut", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChucVu",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ChucVu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CircPlace",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Work_Session = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cir_Type = table.Column<int>(type: "int", nullable: true),
                    Cir_Work_Follow = table.Column<int>(type: "int", nullable: true),
                    Access_Request_Validtime = table.Column<int>(type: "int", nullable: true),
                    Limit_Book = table.Column<int>(type: "int", nullable: true),
                    Vitual = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_CircPlace", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Class",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Class", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "collection",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Allowdownload = table.Column<int>(type: "int", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Share = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_collection", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionPermistionUser",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<long>(type: "bigint", nullable: true),
                    GroupUserId = table.Column<long>(type: "bigint", nullable: true),
                    CanAdd = table.Column<int>(type: "int", nullable: true),
                    CanEdit = table.Column<int>(type: "int", nullable: true),
                    CanDelete = table.Column<int>(type: "int", nullable: true),
                    CanView = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_CollectionPermistionUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "config_aacr2",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bib_Type_Id = table.Column<int>(type: "int", nullable: true),
                    Used_Type_Id = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_config_aacr2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Config_Isbd",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bib_Type_Id = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_Config_Isbd", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigImportReader",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ConfigImportReader", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfigReceiption",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    CheckType = table.Column<int>(type: "int", nullable: true),
                    AutoCheck = table.Column<int>(type: "int", nullable: true),
                    CircPlaceId = table.Column<int>(type: "int", nullable: true),
                    RequirePassword = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ConfigReceiption", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contact",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContactGroupId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Contact", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactGroup",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ContactGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Counter",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Counter = table.Column<long>(type: "bigint", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Counter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Course",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Course", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Course",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Credit = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    DegreeId = table.Column<long>(type: "bigint", nullable: true),
                    OptionCourseId = table.Column<long>(type: "bigint", nullable: true),
                    KnowledgeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Course", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CourseOption",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_CourseOption", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Currency",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExchangeRate = table.Column<double>(type: "float", nullable: true),
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
                    table.PrimaryKey("PK_Currency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Customer",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Customer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "d_bib_status",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Opac_Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    English = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_d_bib_status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "d_key",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_d_key", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "D_Publisher",
                schema: "PrintBook",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Place = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_D_Publisher", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Degree",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Degree", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Degree",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Degree", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DFixField",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Marc_Type_Id = table.Column<long>(type: "bigint", nullable: true),
                    Material_Type_Id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_DFixField", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DFixFieldPost",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostNumber = table.Column<int>(type: "int", nullable: true),
                    PostLengh = table.Column<int>(type: "int", nullable: true),
                    FixFieldId = table.Column<long>(type: "bigint", nullable: true),
                    VnDesciption = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DFixFieldPost", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DFixFieldValue",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixFieldPostId = table.Column<long>(type: "bigint", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DFixFieldValue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DicAuthor",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnsignName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DicAuthor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DicClass",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DicClass", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DicKeyword",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnsignName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DicKeyword", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DicPublisher",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnsignName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DicPublisher", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DigType",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionVn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DigType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Document",
                schema: "EOffice",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Brief = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: true),
                    AgencyId = table.Column<long>(type: "bigint", nullable: true),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TypeId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Sign = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TopicId = table.Column<long>(type: "bigint", nullable: true),
                    GovDocNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Document", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFile",
                schema: "EOffice",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<double>(type: "float", nullable: true),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_DocumentFile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentType",
                schema: "EOffice",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DocumentType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ebook",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    Cardnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Bookid = table.Column<long>(type: "bigint", nullable: true),
                    Page = table.Column<int>(type: "int", nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Ebook", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EbookFavorite",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    Cardnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EbookFavorite", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EbookLog",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    Cardnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Bookid = table.Column<long>(type: "bigint", nullable: true),
                    Page = table.Column<int>(type: "int", nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_EbookLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ethenic",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Ethenic", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventNews",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_EventNews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fixed_field_value",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bibid = table.Column<long>(type: "bigint", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_fixed_field_value", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FrequencyMagazine",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DV = table.Column<long>(type: "bigint", nullable: true),
                    SoTrenDV = table.Column<int>(type: "int", nullable: true),
                    DVTrenSo = table.Column<int>(type: "int", nullable: true),
                    NgayPhatHanh = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_FrequencyMagazine", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fund",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manager = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Blane = table.Column<double>(type: "float", nullable: true),
                    BudgetId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Fund", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeographicAreas",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_GeographicAreas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupReader",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_GroupReader", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GroupUser",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_GroupUser", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntroBookCategory",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLogin = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PageTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_IntroBookCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntroBooks",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brief = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Noidung = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Image = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    IntroBookCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_IntroBooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inventory",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Inventory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryBarcode",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    InventoryId = table.Column<long>(type: "bigint", nullable: true),
                    CheckStoreStatus = table.Column<int>(type: "int", nullable: true),
                    CheckStatus = table.Column<int>(type: "int", nullable: true),
                    CheckBorrow = table.Column<int>(type: "int", nullable: true),
                    CheckRegisteter = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_InventoryBarcode", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "isbd_field",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Config_Id = table.Column<int>(type: "int", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Starttp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stoptp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fieldindex = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_isbd_field", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "isbd_subfield",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Config_Id = table.Column<int>(type: "int", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subfield = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Starttp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stoptp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nexttp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subfieldindex = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_isbd_subfield", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Item",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Item", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemType",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ItemType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Key",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Machiakhoa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tenchiakhoa = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Key", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeyIn",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Keyid = table.Column<long>(type: "bigint", nullable: true),
                    Readerid = table.Column<long>(type: "bigint", nullable: true),
                    BorrowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Userid = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_KeyIn", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeyOut",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Keyid = table.Column<long>(type: "bigint", nullable: true),
                    Readerid = table.Column<long>(type: "bigint", nullable: true),
                    BorrowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Userid = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_KeyOut", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KiemKe",
                schema: "PrintBook",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Dkcb = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreId = table.Column<int>(type: "int", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_KiemKe", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Knowledge",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Knowledge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Language",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Language", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinhVucNghienCuu",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ma = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_LinhVucNghienCuu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Link",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    LinkGroupId = table.Column<long>(type: "bigint", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Link", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinkGroup",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_LinkGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Log_BienMuc",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Log_BienMuc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LostBook",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Barcode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Store = table.Column<long>(type: "bigint", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_LostBook", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lydophat",
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
                    table.PrimaryKey("PK_Lydophat", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MagazineType",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
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
                    table.PrimaryKey("PK_MagazineType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "marc_code_list",
                schema: "PrintBook",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Loai = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ma = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Desvn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Desta = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_marc_code_list", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Marc_Field",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Vndescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Repeatable = table.Column<int>(type: "int", nullable: true),
                    MANDATORY = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_Marc_Field", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marc_Indicator",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    INDICATOR = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VNDESCRIPTION = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Field_Id = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Marc_Indicator", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marc_SubField",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subfield = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Vndescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Repeatable = table.Column<int>(type: "int", nullable: true),
                    MANDATORY = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_Marc_SubField", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarcBibLevel",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MarcBibLevel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarcRecordType",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MarcTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecordTypeCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MarcRecordType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarcType",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MarcType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MaterialsType",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MaterialsType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menu",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MenuType = table.Column<long>(type: "bigint", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FriendUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    OpenType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    LinkType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLogIn = table.Column<int>(type: "int", nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Menu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MenuType",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MenuType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MetaDataFieldRegistery",
                schema: "Ebook",
                columns: table => new
                {
                    MetaDataFieldId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetaDataSchemaId = table.Column<long>(type: "bigint", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subfield = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionVn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Input = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExportField = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_MetaDataFieldRegistery", x => x.MetaDataFieldId);
                });

            migrationBuilder.CreateTable(
                name: "MetadataSchemaRegistry",
                schema: "Ebook",
                columns: table => new
                {
                    MetadataSchemaId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameSpace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MetadataSchemaRegistry", x => x.MetadataSchemaId);
                });

            migrationBuilder.CreateTable(
                name: "MetaDataValue",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MetaDataFieldId = table.Column<int>(type: "int", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    Value_UnSign = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_MetaDataValue", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Module",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Group = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    ModuleCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FolderPage = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Module", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModuleRoles",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RolesId = table.Column<long>(type: "bigint", nullable: true),
                    ModuleId = table.Column<long>(type: "bigint", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Can_access = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_add = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_edit = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_delete = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_view = table.Column<byte>(type: "tinyint", nullable: true),
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
                    table.PrimaryKey("PK_ModuleRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonHoc",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenMon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SoTinChi = table.Column<int>(type: "int", nullable: true),
                    DegreeId = table.Column<long>(type: "bigint", nullable: true),
                    KnowledgeId = table.Column<long>(type: "bigint", nullable: true),
                    OptionId = table.Column<int>(type: "int", nullable: true),
                    NguoiBienSoan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Active = table.Column<int>(type: "int", nullable: true),
                    Attachment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MonHoc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nation",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Nation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "news",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Brief = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Thumb = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    EventId = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Types = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Clourse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    AllowComment = table.Column<int>(type: "int", nullable: true),
                    TotalView = table.Column<int>(type: "int", nullable: true),
                    MetaTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaKeyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaleAudio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FaleAudio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentAudio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BriefAudio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TitleAudio = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_news", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsComment",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewsId = table.Column<long>(type: "bigint", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_NewsComment", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NganhHoc",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MajorsName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    ProgramId = table.Column<long>(type: "bigint", nullable: true),
                    AmountStudent = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MajorsCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_NganhHoc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NganhMonHoc",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MajorId = table.Column<long>(type: "bigint", nullable: true),
                    MonHocId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_NganhMonHoc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Order_Status",
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
                    table.PrimaryKey("PK_Order_Status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Org",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Org", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Page",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PageName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Page", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartemMagazineDetail",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatternId = table.Column<long>(type: "bigint", nullable: true),
                    X = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Y = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Z = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StepX = table.Column<int>(type: "int", nullable: true),
                    StepY = table.Column<int>(type: "int", nullable: true),
                    StepZ = table.Column<int>(type: "int", nullable: true),
                    RepeatX = table.Column<int>(type: "int", nullable: true),
                    RepeatY = table.Column<int>(type: "int", nullable: true),
                    RepeatZ = table.Column<int>(type: "int", nullable: true),
                    MaxX = table.Column<int>(type: "int", nullable: true),
                    MaxY = table.Column<int>(type: "int", nullable: true),
                    MaxZ = table.Column<int>(type: "int", nullable: true),
                    ResetX = table.Column<int>(type: "int", nullable: true),
                    ResetY = table.Column<int>(type: "int", nullable: true),
                    ResetZ = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PartemMagazineDetail", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PatternMagazine",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Function = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PatternMagazine", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permission",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    ModuleId = table.Column<long>(type: "bigint", nullable: true),
                    Can_Access = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_Add = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_Edit = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_Delete = table.Column<byte>(type: "tinyint", nullable: true),
                    Can_View = table.Column<byte>(type: "tinyint", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Permission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhongBan",
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
                    table.PrimaryKey("PK_PhongBan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Photo",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Brief = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Image = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Postion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhotoAlbumId = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Types = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Photo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhotoAlbum",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Image = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    Types = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Postions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSpecial = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PhotoAlbum", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyCirc",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderType = table.Column<int>(type: "int", nullable: true),
                    CircPlace = table.Column<int>(type: "int", nullable: true),
                    NumberOfDate = table.Column<int>(type: "int", nullable: true),
                    NumberOfRenew = table.Column<int>(type: "int", nullable: true),
                    NumberOfBook = table.Column<int>(type: "int", nullable: true),
                    NumberOfRequest = table.Column<int>(type: "int", nullable: true),
                    Muontrung = table.Column<int>(type: "int", nullable: true),
                    Store = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PolicyCirc", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyDigital",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderTypeid = table.Column<int>(type: "int", nullable: true),
                    Maxpage = table.Column<int>(type: "int", nullable: true),
                    Maxsize = table.Column<double>(type: "float", nullable: true),
                    Maxdocument = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PolicyDigital", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PolicyDigitalByCollection",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderTypeid = table.Column<int>(type: "int", nullable: true),
                    CollectionId = table.Column<int>(type: "int", nullable: true),
                    Maxpage = table.Column<int>(type: "int", nullable: true),
                    Maxsize = table.Column<double>(type: "float", nullable: true),
                    Maxdocument = table.Column<int>(type: "int", nullable: true),
                    Read = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<int>(type: "int", nullable: true),
                    Download = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PolicyDigitalByCollection", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PrintBookandDigital",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
                    EbookId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_PrintBookandDigital", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Private",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    ModuleId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_Private", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prof",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Prof", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Program",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Program", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reader",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cardno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrgId = table.Column<long>(type: "bigint", nullable: true),
                    ReaderTypeId = table.Column<long>(type: "bigint", nullable: true),
                    ClassId = table.Column<long>(type: "bigint", nullable: true),
                    CourseId = table.Column<long>(type: "bigint", nullable: true),
                    DegreeId = table.Column<long>(type: "bigint", nullable: true),
                    EthenicId = table.Column<long>(type: "bigint", nullable: true),
                    ProfId = table.Column<long>(type: "bigint", nullable: true),
                    Blane = table.Column<double>(type: "float", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LasttimeLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Sex = table.Column<int>(type: "int", nullable: true),
                    RequiredChangePassword = table.Column<int>(type: "int", nullable: true),
                    IsChangePassword = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_Reader", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderDelete",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cardno = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrgId = table.Column<long>(type: "bigint", nullable: true),
                    ReaderTypeId = table.Column<long>(type: "bigint", nullable: true),
                    ClassId = table.Column<long>(type: "bigint", nullable: true),
                    CourseId = table.Column<long>(type: "bigint", nullable: true),
                    DegreeId = table.Column<long>(type: "bigint", nullable: true),
                    EthenicId = table.Column<long>(type: "bigint", nullable: true),
                    ProfId = table.Column<long>(type: "bigint", nullable: true),
                    Blane = table.Column<double>(type: "float", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpireDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LasttimeLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastLogin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Sex = table.Column<int>(type: "int", nullable: true),
                    RequiredChangePassword = table.Column<int>(type: "int", nullable: true),
                    IsChangePassword = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ReaderDelete", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderInGroup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    GroupReaderId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_ReaderInGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderTrackingLogin",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: true),
                    Cardnumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoginTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogOutTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_ReaderTrackingLogin", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderType",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_ReaderType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Receipt_Status",
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
                    table.PrimaryKey("PK_Receipt_Status", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RECORDTYPE",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VnDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_RECORDTYPE", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Review",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Review", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    App = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Serial",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FrequencyId = table.Column<long>(type: "bigint", nullable: true),
                    PatternId = table.Column<long>(type: "bigint", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastX = table.Column<int>(type: "int", nullable: true),
                    LastY = table.Column<int>(type: "int", nullable: true),
                    LastZ = table.Column<int>(type: "int", nullable: true),
                    IssueLength = table.Column<int>(type: "int", nullable: true),
                    WeekLength = table.Column<int>(type: "int", nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    MonthLenght = table.Column<int>(type: "int", nullable: true),
                    Locate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FirstTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Approved = table.Column<bool>(type: "bit", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Serial", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SerialBinding",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccessionNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
                    VolumeTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubscriptionId = table.Column<long>(type: "bigint", nullable: true),
                    SubscriptionTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BindingDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SerialBinding", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SerialBindingItem",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BindingId = table.Column<long>(type: "bigint", nullable: true),
                    IssueId = table.Column<long>(type: "bigint", nullable: true),
                    SerialSeq = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SerialBindingItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SERIALITEM",
                schema: "PrintBook",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SUBSCRIPTION_ID = table.Column<long>(type: "bigint", nullable: true),
                    SERIAL_SEQ = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SERIAL_SEQ_X = table.Column<int>(type: "int", nullable: true),
                    SERIAL_SEQ_Y = table.Column<int>(type: "int", nullable: true),
                    SERIAL_SEQ_Z = table.Column<int>(type: "int", nullable: true),
                    IS_SPECIAL = table.Column<int>(type: "int", nullable: true),
                    STATUS = table.Column<int>(type: "int", nullable: true),
                    QUANTITY = table.Column<int>(type: "int", nullable: true),
                    PLANNED_DATE = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NOTE = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PUBLISHED_DATE = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CLAIM_DATE = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CLAIM_COUNT = table.Column<int>(type: "int", nullable: true),
                    IsMerged = table.Column<bool>(type: "bit", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_SERIALITEM", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "Sip2Log",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Request = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Response = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_Sip2Log", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Store",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Postion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StoreTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Store", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreType",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_StoreType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubcriptionStatus",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_SubcriptionStatus", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subject",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    DDC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Subject", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Supplier",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Account = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mst = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Website = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Representative = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Supplier", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Supportonline",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Yahoo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Skype = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Facebook = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mobile = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Wellcome = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Supportonline", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "System_description",
                schema: "PrintBook",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descvn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Val = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_System_description", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SystemInfo",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LibraryName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tel = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_SystemInfo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemPara",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_SystemPara", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "systemparameter",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionVn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_systemparameter", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaiLieu",
                schema: "Evaluate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BibId = table.Column<long>(type: "bigint", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoaiTaiLieu = table.Column<int>(type: "int", nullable: true),
                    LanXuatBan = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EBookId = table.Column<long>(type: "bigint", nullable: true),
                    MonHocId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_TaiLieu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenant",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenant", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "thanhly",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BarcodeId = table.Column<long>(type: "bigint", nullable: true),
                    Sumited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_thanhly", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TheodoiBienmucEbook",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DigId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_TheodoiBienmucEbook", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Topic",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLogin = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PageTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DDC = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Topic", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Topic",
                schema: "EOffice",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Level = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Topic", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trackingtolibrary",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Readerid = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Time = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoreId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_trackingtolibrary", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserLog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Object = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ip = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Application = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoginName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoleId = table.Column<int>(type: "int", nullable: true),
                    PostionId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: true),
                    Sex = table.Column<int>(type: "int", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Photo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RoleWinformId = table.Column<long>(type: "bigint", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Video",
                schema: "cms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Link = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Submited = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VideoType = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Video", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worksheet_field",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Bib_Worksheet_Id = table.Column<int>(type: "int", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    L1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    L2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_worksheet_field", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worksheet_subfield",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Worksheet_Field_Id = table.Column<long>(type: "bigint", nullable: true),
                    Subfield = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_worksheet_subfield", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Z3950Config",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Port = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DatabaseName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Systax = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GroupId = table.Column<long>(type: "bigint", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Z3950Config", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Z3950Group",
                schema: "PrintBook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Z3950Group", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Item",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Submited = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateBy = table.Column<long>(type: "bigint", nullable: true),
                    CollectionId = table.Column<long>(type: "bigint", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalView = table.Column<int>(type: "int", nullable: true),
                    TotalDownload = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: true),
                    SubjectId = table.Column<long>(type: "bigint", nullable: true),
                    LastUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TypeId = table.Column<long>(type: "bigint", nullable: true),
                    AllowDownload = table.Column<int>(type: "int", nullable: true),
                    Free = table.Column<int>(type: "int", nullable: true),
                    TopicId = table.Column<long>(type: "bigint", nullable: true),
                    PortalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Language = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Show = table.Column<int>(type: "int", nullable: true),
                    IndexContent = table.Column<int>(type: "int", nullable: true),
                    Share = table.Column<int>(type: "int", nullable: true),
                    FileVersion = table.Column<int>(type: "int", nullable: true),
                    IndexedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_Item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Item_Subject_SubjectId",
                        column: x => x.SubjectId,
                        principalSchema: "Ebook",
                        principalTable: "Subject",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Item_Topic_TopicId",
                        column: x => x.TopicId,
                        principalSchema: "Ebook",
                        principalTable: "Topic",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Item_collection_CollectionId",
                        column: x => x.CollectionId,
                        principalSchema: "Ebook",
                        principalTable: "collection",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EbookFile",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsConvert = table.Column<int>(type: "int", nullable: true),
                    EbookId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FileType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSize = table.Column<double>(type: "float", nullable: true),
                    FileExt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormatId = table.Column<int>(type: "int", nullable: true),
                    CheckSumAlgorithm = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_EbookFile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EbookFile_Item_EbookId",
                        column: x => x.EbookId,
                        principalSchema: "Ebook",
                        principalTable: "Item",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "itemXml",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Keyword = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Xml = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtherTitle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Page = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OldAuthor = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_itemXml", x => x.Id);
                    table.ForeignKey(
                        name: "FK_itemXml_Item_Id",
                        column: x => x.Id,
                        principalSchema: "Ebook",
                        principalTable: "Item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EbookFile_EbookId",
                schema: "Ebook",
                table: "EbookFile",
                column: "EbookId");

            migrationBuilder.CreateIndex(
                name: "IX_Item_CollectionId",
                schema: "Ebook",
                table: "Item",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Item_SubjectId",
                schema: "Ebook",
                table: "Item",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Item_TopicId",
                schema: "Ebook",
                table: "Item",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "aacr2_field",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "aacr2_subfield",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_deliverer",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_deliverer_detail",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_deliverer_status",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_move",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_move_detail",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_order",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_order_detail",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_receipt",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_receipt_detail",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ab_source",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ADS",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "ADSGroup",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Agency",
                schema: "EOffice");

            migrationBuilder.DropTable(
                name: "ah_receipt",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "AttachFile",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Banner",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Barcode",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Barcode_Status",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Bib",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Bib_Type",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "bib_worksheet",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BibData",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BibDataOrder",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BibOrder",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BibXML",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BibXMLOrder",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Book_Group",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "book_group_detail",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BookIn",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BookOut",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "BookRequest",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Budget",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "C_Fine",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "c_fine_method",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "C_Fine_type",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "C_photo",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "c_queue_status",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "C_renew",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "C_renew_data",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Cabinet",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Category",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "CheckIn",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "CheckOut",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ChucVu",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CircPlace",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Class",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CollectionPermistionUser",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "config_aacr2",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Config_Isbd",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "ConfigImportReader",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ConfigReceiption",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Contact",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "ContactGroup",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Counter",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Countries",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Course",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Course",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "CourseOption",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "Currency",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Customer",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "d_bib_status",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "d_key",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "D_Publisher",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Degree",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Degree",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "DFixField",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DFixFieldPost",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DFixFieldValue",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DicAuthor",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DicClass",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DicKeyword",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DicPublisher",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "DigType",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Document",
                schema: "EOffice");

            migrationBuilder.DropTable(
                name: "DocumentFile",
                schema: "EOffice");

            migrationBuilder.DropTable(
                name: "DocumentType",
                schema: "EOffice");

            migrationBuilder.DropTable(
                name: "Ebook",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "EbookFavorite",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "EbookFile",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "EbookLog",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Ethenic",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EventNews",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "fixed_field_value",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "FrequencyMagazine",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Fund",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "GeographicAreas",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "GroupReader",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GroupUser",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IntroBookCategory",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "IntroBooks",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Inventory",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "InventoryBarcode",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "isbd_field",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "isbd_subfield",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Item",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "ItemType",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "itemXml",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Key",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "KeyIn",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "KeyOut",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "KiemKe",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Knowledge",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "Language",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "LinhVucNghienCuu",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Link",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "LinkGroup",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Log_BienMuc",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "LostBook",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Lydophat",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "MagazineType",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "marc_code_list",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Marc_Field",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Marc_Indicator",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Marc_SubField",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "MarcBibLevel",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "MarcRecordType",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "MarcType",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "MaterialsType",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Menu",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "MenuType",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "MetaDataFieldRegistery",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "MetadataSchemaRegistry",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "MetaDataValue",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Module",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "ModuleRoles",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "MonHoc",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "Nation",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "news",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "NewsComment",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "NganhHoc",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "NganhMonHoc",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "Order_Status",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Org",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Page",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "PartemMagazineDetail",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "PatternMagazine",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Permission",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "PhongBan",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Photo",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "PhotoAlbum",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "PolicyCirc",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "PolicyDigital",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "PolicyDigitalByCollection",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "PrintBookandDigital",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Private",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Prof",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Program",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "Reader",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReaderDelete",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReaderInGroup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReaderTrackingLogin",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReaderType",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Receipt_Status",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "RECORDTYPE",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Review",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Serial",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "SerialBinding",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "SerialBindingItem",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "SERIALITEM",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Sip2Log",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Store",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "StoreType",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "SubcriptionStatus",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Supplier",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Supportonline",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "System_description",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "SystemInfo",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "SystemPara",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "systemparameter",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TaiLieu",
                schema: "Evaluate");

            migrationBuilder.DropTable(
                name: "Tenant",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "thanhly",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "TheodoiBienmucEbook",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Topic",
                schema: "EOffice");

            migrationBuilder.DropTable(
                name: "trackingtolibrary",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "UserLog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Video",
                schema: "cms");

            migrationBuilder.DropTable(
                name: "worksheet_field",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "worksheet_subfield",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Z3950Config",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Z3950Group",
                schema: "PrintBook");

            migrationBuilder.DropTable(
                name: "Item",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Subject",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "Topic",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "collection",
                schema: "Ebook");
        }
    }
}
