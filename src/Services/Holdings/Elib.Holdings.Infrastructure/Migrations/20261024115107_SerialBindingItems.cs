using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Holdings.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SerialBindingItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "serial_binding_public_id",
                table: "items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_items_tenant_id_serial_binding_public_id",
                table: "items",
                columns: new[] { "tenant_id", "serial_binding_public_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_items_tenant_id_serial_binding_public_id",
                table: "items");

            migrationBuilder.DropColumn(
                name: "serial_binding_public_id",
                table: "items");
        }
    }
}
