using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedFieldValueOrderEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OrderDetailId",
                schema: "PrintBook",
                table: "ab_receipt_detail",
                type: "bigint",
                nullable: true);

            // KHÔNG CreateTable "fixed_field_value_order" — bảng này đã tồn tại sẵn trong DB thật (tạo
            // ngoài luồng migration), cấu trúc cột đã kiểm chứng giống hệt "fixed_field_value". Migration
            // này chỉ đăng ký entity FixedFieldValueOrder vào model, không đụng tới bảng thật.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OrderDetailId",
                schema: "PrintBook",
                table: "ab_receipt_detail");
        }
    }
}
