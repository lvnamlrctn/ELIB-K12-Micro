using Elib.BuildingBlocks.Persistence;
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Elib.Serials.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSerials : Migration
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
                    publisher = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    issn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    title_key = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    deleted = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bib_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "frequencies",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    unit = table.Column<int>(type: "integer", nullable: false),
                    issues_per_unit = table.Column<int>(type: "integer", nullable: true),
                    units_per_issue = table.Column<int>(type: "integer", nullable: true),
                    release_day = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_frequencies", x => x.id);
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
                name: "numbering_patterns",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    label_template = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    name_x = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name_y = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    name_z = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    step_x = table.Column<int>(type: "integer", nullable: false),
                    step_y = table.Column<int>(type: "integer", nullable: false),
                    step_z = table.Column<int>(type: "integer", nullable: false),
                    max_x = table.Column<int>(type: "integer", nullable: true),
                    max_y = table.Column<int>(type: "integer", nullable: true),
                    max_z = table.Column<int>(type: "integer", nullable: true),
                    reset_x = table.Column<int>(type: "integer", nullable: false),
                    reset_y = table.Column<int>(type: "integer", nullable: false),
                    reset_z = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_numbering_patterns", x => x.id);
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
                name: "publication_types",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    public_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    tenant_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_publication_types", x => x.id);
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
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    bib_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bib_mfn = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    issn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    publication_type_id = table.Column<long>(type: "bigint", nullable: true),
                    frequency_id = table.Column<long>(type: "bigint", nullable: true),
                    pattern_id = table.Column<long>(type: "bigint", nullable: true),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    store_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    supplier_public_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supplier_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    first_issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    start_x = table.Column<int>(type: "integer", nullable: true),
                    start_y = table.Column<int>(type: "integer", nullable: true),
                    start_z = table.Column<int>(type: "integer", nullable: true),
                    last_x = table.Column<int>(type: "integer", nullable: true),
                    last_y = table.Column<int>(type: "integer", nullable: true),
                    last_z = table.Column<int>(type: "integer", nullable: true),
                    call_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    public_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    internal_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    grace_days = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    approved = table.Column<bool>(type: "boolean", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("pk_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_subscriptions_frequencies_frequency_id",
                        column: x => x.frequency_id,
                        principalTable: "frequencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_numbering_patterns_pattern_id",
                        column: x => x.pattern_id,
                        principalTable: "numbering_patterns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_subscriptions_publication_types_publication_type_id",
                        column: x => x.publication_type_id,
                        principalTable: "publication_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "bindings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    accession_no = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    volume_title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    binding_date = table.Column<DateOnly>(type: "date", nullable: false),
                    store_id = table.Column<long>(type: "bigint", nullable: true),
                    store_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    item_public_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_bindings", x => x.id);
                    table.ForeignKey(
                        name: "fk_bindings_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "serial_issues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    subscription_id = table.Column<long>(type: "bigint", nullable: false),
                    label = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    x = table.Column<int>(type: "integer", nullable: true),
                    y = table.Column<int>(type: "integer", nullable: true),
                    z = table.Column<int>(type: "integer", nullable: true),
                    is_special = table.Column<bool>(type: "boolean", nullable: false),
                    is_merged = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: true),
                    received_date = table.Column<DateOnly>(type: "date", nullable: true),
                    claim_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claim_count = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_serial_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_serial_issues_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "binding_issues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    binding_id = table.Column<long>(type: "bigint", nullable: false),
                    issue_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("pk_binding_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_binding_issues_bindings_binding_id",
                        column: x => x.binding_id,
                        principalTable: "bindings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_binding_issues_serial_issues_issue_id",
                        column: x => x.issue_id,
                        principalTable: "serial_issues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "ix_binding_issues_binding_id",
                table: "binding_issues",
                column: "binding_id");

            migrationBuilder.CreateIndex(
                name: "ix_binding_issues_issue_id",
                table: "binding_issues",
                column: "issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_binding_issues_public_id",
                table: "binding_issues",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_binding_issues_tenant_id",
                table: "binding_issues",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_binding_issues_tenant_id_issue_id",
                table: "binding_issues",
                columns: new[] { "tenant_id", "issue_id" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_bindings_public_id",
                table: "bindings",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bindings_subscription_id",
                table: "bindings",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_bindings_tenant_id",
                table: "bindings",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_bindings_tenant_id_accession_no",
                table: "bindings",
                columns: new[] { "tenant_id", "accession_no" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_frequencies_public_id",
                table: "frequencies",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_frequencies_tenant_id",
                table: "frequencies",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_frequencies_tenant_id_name",
                table: "frequencies",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_state_delivered",
                schema: "masstransit",
                table: "inbox_state",
                column: "delivered");

            migrationBuilder.CreateIndex(
                name: "ix_numbering_patterns_public_id",
                table: "numbering_patterns",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_numbering_patterns_tenant_id",
                table: "numbering_patterns",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_numbering_patterns_tenant_id_name",
                table: "numbering_patterns",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_deleted = false");

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
                name: "ix_publication_types_public_id",
                table: "publication_types",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_publication_types_tenant_id",
                table: "publication_types",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_publication_types_tenant_id_name",
                table: "publication_types",
                columns: new[] { "tenant_id", "name" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_serial_issues_public_id",
                table: "serial_issues",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_serial_issues_subscription_id",
                table: "serial_issues",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "ix_serial_issues_tenant_id",
                table: "serial_issues",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_serial_issues_tenant_id_subscription_id_status",
                table: "serial_issues",
                columns: new[] { "tenant_id", "subscription_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_serial_issues_tenant_id_subscription_id_x_y_z",
                table: "serial_issues",
                columns: new[] { "tenant_id", "subscription_id", "x", "y", "z" },
                unique: true,
                filter: "is_deleted = false AND is_merged = false");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_frequency_id",
                table: "subscriptions",
                column: "frequency_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_pattern_id",
                table: "subscriptions",
                column: "pattern_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_public_id",
                table: "subscriptions",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_publication_type_id",
                table: "subscriptions",
                column: "publication_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id",
                table: "subscriptions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_tenant_id_bib_public_id",
                table: "subscriptions",
                columns: new[] { "tenant_id", "bib_public_id" });

            // Lớp bảo vệ thứ hai (docs 04 §2.1): mọi bảng của đơn vị bật RLS.
            foreach (var table in new[] { "publication_types", "frequencies", "numbering_patterns", "bib_snapshots", "subscriptions", "serial_issues", "bindings",
                         "binding_issues" })
                migrationBuilder.EnableTenantRls(table);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bib_snapshots");

            migrationBuilder.DropTable(
                name: "binding_issues");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "masstransit");

            migrationBuilder.DropTable(
                name: "tenant_module_replicas",
                schema: "replica");

            migrationBuilder.DropTable(
                name: "bindings");

            migrationBuilder.DropTable(
                name: "serial_issues");

            migrationBuilder.DropTable(
                name: "inbox_state",
                schema: "masstransit");

            migrationBuilder.DropTable(
                name: "outbox_state",
                schema: "masstransit");

            migrationBuilder.DropTable(
                name: "tenant_replicas",
                schema: "replica");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "frequencies");

            migrationBuilder.DropTable(
                name: "numbering_patterns");

            migrationBuilder.DropTable(
                name: "publication_types");
        }
    }
}
