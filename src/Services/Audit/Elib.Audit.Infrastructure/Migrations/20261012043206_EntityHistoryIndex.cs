using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elib.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EntityHistoryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_id_entity_type_entity_id",
                table: "audit_logs",
                columns: new[] { "tenant_id", "entity_type", "entity_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_audit_logs_tenant_id_entity_type_entity_id",
                table: "audit_logs");
        }
    }
}
