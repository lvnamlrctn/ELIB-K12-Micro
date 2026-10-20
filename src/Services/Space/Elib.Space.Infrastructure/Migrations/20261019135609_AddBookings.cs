using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Space.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "room_bookings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    space_id = table.Column<long>(type: "bigint", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    party_size = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_in_by = table.Column<long>(type: "bigint", nullable: true),
                    checked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_out_by = table.Column<long>(type: "bigint", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_room_bookings", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_bookings_bookable_spaces_space_id",
                        column: x => x.space_id,
                        principalTable: "bookable_spaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_booking_members",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("pk_room_booking_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_room_booking_members_room_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "room_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_room_booking_members_booking_id",
                table: "room_booking_members",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_booking_members_public_id",
                table: "room_booking_members",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_booking_members_tenant_id",
                table: "room_booking_members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_booking_members_tenant_id_booking_id",
                table: "room_booking_members",
                columns: new[] { "tenant_id", "booking_id" });

            migrationBuilder.CreateIndex(
                name: "ix_room_booking_members_tenant_id_reader_public_id",
                table: "room_booking_members",
                columns: new[] { "tenant_id", "reader_public_id" });

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_public_id",
                table: "room_bookings",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_space_id",
                table: "room_bookings",
                column: "space_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_tenant_id",
                table: "room_bookings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_tenant_id_card_no",
                table: "room_bookings",
                columns: new[] { "tenant_id", "card_no" });

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_tenant_id_reader_public_id_status",
                table: "room_bookings",
                columns: new[] { "tenant_id", "reader_public_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_room_bookings_tenant_id_space_id_start_at_end_at",
                table: "room_bookings",
                columns: new[] { "tenant_id", "space_id", "start_at", "end_at" });

            migrationBuilder.EnableTenantRls("room_bookings");
            migrationBuilder.EnableTenantRls("room_booking_members");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "room_booking_members");

            migrationBuilder.DropTable(
                name: "room_bookings");
        }
    }
}
