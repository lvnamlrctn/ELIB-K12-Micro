using System;
using Elib.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Search.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "masstransit");

            migrationBuilder.EnsureSchema(
                name: "replica");

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
                name: "search_bibs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    bib_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mfn = table.Column<long>(type: "bigint", nullable: false),
                    bib_type_id = table.Column<long>(type: "bigint", nullable: true),
                    material_type = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    author = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    other_authors = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    publisher = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    publish_place = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    publish_year = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    year = table.Column<int>(type: "integer", nullable: true),
                    isbns = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ddc = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cutter = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    keywords = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    edition = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    physical_description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    series = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    title_fold = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    author_fold = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    publisher_fold = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    keyword_fold = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    isbn_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    search_text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sync_run = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_bibs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_items",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    item_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bib_public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mfn = table.Column<long>(type: "bigint", nullable: false),
                    barcode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    barcode_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    store_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    status = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    loan_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    loan_version = table.Column<long>(type: "bigint", nullable: false),
                    loaned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    loan_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    on_loan = table.Column<bool>(type: "boolean", nullable: false),
                    sync_run = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "search_sync_states",
                columns: table => new
                {
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    bibs = table.Column<int>(type: "integer", nullable: false),
                    items = table.Column<int>(type: "integer", nullable: false),
                    loans = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_sync_states", x => x.tenant_id);
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
                name: "ix_inbox_state_delivered",
                schema: "masstransit",
                table: "inbox_state",
                column: "delivered");

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
                name: "ix_search_bibs_tenant_id",
                table: "search_bibs",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_bibs_tenant_id_bib_public_id",
                table: "search_bibs",
                columns: new[] { "tenant_id", "bib_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_bibs_tenant_id_mfn",
                table: "search_bibs",
                columns: new[] { "tenant_id", "mfn" });

            migrationBuilder.CreateIndex(
                name: "ix_search_bibs_tenant_id_status_deleted_year",
                table: "search_bibs",
                columns: new[] { "tenant_id", "status", "deleted", "year" });

            migrationBuilder.CreateIndex(
                name: "ix_search_items_tenant_id",
                table: "search_items",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_search_items_tenant_id_barcode_key",
                table: "search_items",
                columns: new[] { "tenant_id", "barcode_key" });

            migrationBuilder.CreateIndex(
                name: "ix_search_items_tenant_id_item_public_id",
                table: "search_items",
                columns: new[] { "tenant_id", "item_public_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_items_tenant_id_mfn",
                table: "search_items",
                columns: new[] { "tenant_id", "mfn" });

            migrationBuilder.CreateIndex(
                name: "ix_search_sync_states_tenant_id",
                table: "search_sync_states",
                column: "tenant_id");

            // Tìm không dấu bằng LIKE '%từ%' trên chữ đã bỏ dấu: chỉ mục trigram (pg_trgm là extension "trusted" — chủ DB tạo được).
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            foreach (var column in new[] { "search_text", "title_fold", "author_fold" })
                migrationBuilder.Sql($"CREATE INDEX ix_search_bibs_{column}_trgm ON search_bibs USING gin ({column} gin_trgm_ops);");

            foreach (var table in new[] { "search_bibs", "search_items", "search_sync_states" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "masstransit");

            migrationBuilder.DropTable(
                name: "search_bibs");

            migrationBuilder.DropTable(
                name: "search_items");

            migrationBuilder.DropTable(
                name: "search_sync_states");

            migrationBuilder.DropTable(
                name: "tenant_module_replicas",
                schema: "replica");

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
