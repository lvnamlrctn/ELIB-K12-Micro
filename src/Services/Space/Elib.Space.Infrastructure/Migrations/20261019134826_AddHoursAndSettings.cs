using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHoursAndSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_opening_hours",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category = table.Column<int>(type: "integer", nullable: true),
                    weekday = table.Column<int>(type: "integer", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_opening_hours", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "room_special_days",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: true),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_special_days", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "space_booking_settings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    default_open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    default_close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    max_per_day = table.Column<int>(type: "integer", nullable: false),
                    max_active = table.Column<int>(type: "integer", nullable: false),
                    max_session_minutes = table.Column<int>(type: "integer", nullable: false),
                    no_show_limit = table.Column<int>(type: "integer", nullable: false),
                    no_show_window_days = table.Column<int>(type: "integer", nullable: false),
                    ban_days = table.Column<int>(type: "integer", nullable: false),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_space_booking_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_room_opening_hours_public_id",
                table: "room_opening_hours",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_opening_hours_tenant_id",
                table: "room_opening_hours",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_opening_hours_tenant_id_category_weekday",
                table: "room_opening_hours",
                columns: new[] { "tenant_id", "category", "weekday" });

            migrationBuilder.CreateIndex(
                name: "ix_room_special_days_public_id",
                table: "room_special_days",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_special_days_tenant_id",
                table: "room_special_days",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_special_days_tenant_id_date_category",
                table: "room_special_days",
                columns: new[] { "tenant_id", "date", "category" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_space_booking_settings_public_id",
                table: "space_booking_settings",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_space_booking_settings_tenant_id",
                table: "space_booking_settings",
                column: "tenant_id",
                unique: true);

            migrationBuilder.EnableTenantRls("room_opening_hours");
            migrationBuilder.EnableTenantRls("room_special_days");
            migrationBuilder.EnableTenantRls("space_booking_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_opening_hours");

            migrationBuilder.DropTable(
                name: "room_special_days");

            migrationBuilder.DropTable(
                name: "space_booking_settings");
        }
    }
}
