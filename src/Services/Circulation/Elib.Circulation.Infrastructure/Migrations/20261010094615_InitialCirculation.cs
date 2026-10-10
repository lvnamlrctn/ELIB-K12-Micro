using System;
using Elib.BuildingBlocks.Persistence;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Circulation.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCirculation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "masstransit");

            migrationBuilder.EnsureSchema(
                name: "replica");

            migrationBuilder.CreateTable(
                name: "bib_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    bib_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mfn = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    author = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ddc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bib_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "circ_places",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    store_ids = table.Column<List<long>>(type: "bigint[]", nullable: false),
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
                    table.PrimaryKey("pk_circ_places", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_state",
                schema: "masstransit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lock_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    receive_count = table.Column<int>(type: "integer", nullable: false),
                    expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_state", x => x.id);
                    table.UniqueConstraint("ak_inbox_state_message_id_consumer_id", x => new { x.message_id, x.consumer_id });
                });

            migrationBuilder.CreateTable(
                name: "item_replicas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    item_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    barcode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    bib_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mfn = table.Column<long>(type: "bigint", nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    store_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_replicas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loans",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    item_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    mfn = table.Column<long>(type: "bigint", nullable: false),
                    circ_place_id = table.Column<long>(type: "bigint", nullable: true),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    loaned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    loaned_by = table.Column<long>(type: "bigint", nullable: true),
                    returned_by = table.Column<long>(type: "bigint", nullable: true),
                    return_circ_place_id = table.Column<long>(type: "bigint", nullable: true),
                    renew_count = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_loans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_state",
                schema: "masstransit",
                columns: table => new
                {
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lock_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_sequence_number = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_state", x => x.outbox_id);
                });

            migrationBuilder.CreateTable(
                name: "patron_replicas",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    reader_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    card_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    full_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    reader_type_id = table.Column<long>(type: "bigint", nullable: true),
                    reader_type_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    class_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    course_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    photo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    expire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_patron_replicas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_replicas",
                schema: "replica",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subdomain = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_version = table.Column<long>(type: "bigint", nullable: false),
                    license_version = table.Column<long>(type: "bigint", nullable: false),
                    synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_replicas", x => x.tenant_id);
                });

            migrationBuilder.CreateTable(
                name: "loan_policies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reader_type_id = table.Column<long>(type: "bigint", nullable: true),
                    circ_place_id = table.Column<long>(type: "bigint", nullable: true),
                    loan_days = table.Column<int>(type: "integer", nullable: false),
                    max_loans = table.Column<int>(type: "integer", nullable: true),
                    max_renewals = table.Column<int>(type: "integer", nullable: true),
                    renew_days = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_loan_policies", x => x.id);
                    table.ForeignKey(
                        name: "fk_loan_policies_circ_places_circ_place_id",
                        column: x => x.circ_place_id,
                        principalTable: "circ_places",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "masstransit",
                columns: table => new
                {
                    sequence_number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    enqueue_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    sent_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    headers = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    inbox_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    inbox_consumer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    outbox_id = table.Column<Guid>(type: "uuid", nullable: true),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    message_type = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    initiator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    destination_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    response_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fault_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    expiration_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_message", x => x.sequence_number);
                    table.ForeignKey(
                        name: "fk_outbox_message_inbox_state_inbox_message_id_inbox_consumer_",
                        columns: x => new { x.inbox_message_id, x.inbox_consumer_id },
                        principalSchema: "masstransit",
                        principalTable: "inbox_state",
                        principalColumns: new[] { "message_id", "consumer_id" });
                    table.ForeignKey(
                        name: "fk_outbox_message_outbox_state_outbox_id",
                        column: x => x.outbox_id,
                        principalSchema: "masstransit",
                        principalTable: "outbox_state",
                        principalColumn: "outbox_id");
                });

            migrationBuilder.CreateTable(
                name: "tenant_module_replicas",
                schema: "replica",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    module_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valid_from = table.Column<DateOnly>(type: "date", nullable: true),
                    valid_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_module_replicas", x => new { x.tenant_id, x.module_code });
                    table.ForeignKey(
                        name: "fk_tenant_module_replicas_tenant_replicas_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "replica",
                        principalTable: "tenant_replicas",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bib_snapshots_tenant_id",
                table: "bib_snapshots",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_bib_snapshots_tenant_id_bib_public_id",
                table: "bib_snapshots",
                columns: new[] { "tenant_id", "bib_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bib_snapshots_tenant_id_mfn",
                table: "bib_snapshots",
                columns: new[] { "tenant_id", "mfn" });

            migrationBuilder.CreateIndex(
                name: "ix_circ_places_public_id",
                table: "circ_places",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_circ_places_tenant_id",
                table: "circ_places",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_circ_places_tenant_id_code",
                table: "circ_places",
                columns: new[] { "tenant_id", "code" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_state_delivered",
                schema: "masstransit",
                table: "inbox_state",
                column: "delivered");

            migrationBuilder.CreateIndex(
                name: "ix_item_replicas_tenant_id",
                table: "item_replicas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_item_replicas_tenant_id_barcode_key",
                table: "item_replicas",
                columns: new[] { "tenant_id", "barcode_key" });

            migrationBuilder.CreateIndex(
                name: "ix_item_replicas_tenant_id_item_public_id",
                table: "item_replicas",
                columns: new[] { "tenant_id", "item_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loan_policies_circ_place_id",
                table: "loan_policies",
                column: "circ_place_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_policies_public_id",
                table: "loan_policies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loan_policies_tenant_id",
                table: "loan_policies",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_policies_tenant_id_reader_type_id_circ_place_id",
                table: "loan_policies",
                columns: new[] { "tenant_id", "reader_type_id", "circ_place_id" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_public_id",
                table: "loans",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id",
                table: "loans",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id_barcode",
                table: "loans",
                columns: new[] { "tenant_id", "barcode" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id_card_no",
                table: "loans",
                columns: new[] { "tenant_id", "card_no" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id_due_at",
                table: "loans",
                columns: new[] { "tenant_id", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id_item_public_id",
                table: "loans",
                columns: new[] { "tenant_id", "item_public_id" },
                unique: true,
                filter: "returned_at IS NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id_loaned_at",
                table: "loans",
                columns: new[] { "tenant_id", "loaned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_loans_tenant_id_reader_public_id_returned_at",
                table: "loans",
                columns: new[] { "tenant_id", "reader_public_id", "returned_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_enqueue_time",
                schema: "masstransit",
                table: "outbox_message",
                column: "enqueue_time");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_expiration_time",
                schema: "masstransit",
                table: "outbox_message",
                column: "expiration_time");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_inbox_message_id_inbox_consumer_id_sequence_",
                schema: "masstransit",
                table: "outbox_message",
                columns: new[] { "inbox_message_id", "inbox_consumer_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_outbox_id_sequence_number",
                schema: "masstransit",
                table: "outbox_message",
                columns: new[] { "outbox_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_state_created",
                schema: "masstransit",
                table: "outbox_state",
                column: "created");

            migrationBuilder.CreateIndex(
                name: "ix_patron_replicas_tenant_id",
                table: "patron_replicas",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_patron_replicas_tenant_id_card_no",
                table: "patron_replicas",
                columns: new[] { "tenant_id", "card_no" });

            migrationBuilder.CreateIndex(
                name: "ix_patron_replicas_tenant_id_reader_public_id",
                table: "patron_replicas",
                columns: new[] { "tenant_id", "reader_public_id" },
                unique: true);
            // Lớp bảo vệ thứ hai (docs 04 §2.1): mọi bảng của đơn vị bật RLS.
            foreach (var table in new[] { "circ_places", "loan_policies", "loans", "patron_replicas", "item_replicas", "bib_snapshots" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bib_snapshots");

            migrationBuilder.DropTable(
                name: "item_replicas");

            migrationBuilder.DropTable(
                name: "loan_policies");

            migrationBuilder.DropTable(
                name: "loans");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "masstransit");

            migrationBuilder.DropTable(
                name: "patron_replicas");

            migrationBuilder.DropTable(
                name: "tenant_module_replicas",
                schema: "replica");

            migrationBuilder.DropTable(
                name: "circ_places");

            migrationBuilder.DropTable(
                name: "inbox_state",
                schema: "masstransit");

            migrationBuilder.DropTable(
                name: "outbox_state",
                schema: "masstransit");

            migrationBuilder.DropTable(
                name: "tenant_replicas",
                schema: "replica");
        }
    }
}
