using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELIBAPI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncGamificationNotificationRoomBookingAndUntrackedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Gamification");

            migrationBuilder.AlterColumn<long>(
                name: "DocGroupId",
                schema: "PrintBook",
                table: "PolicyCircDocGroup",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "Badge",
                schema: "Gamification",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IconName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriteriaType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Threshold = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_Badge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentSubmission",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Author = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Abstract = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentSubmission", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItemReservation",
                schema: "Ebook",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EbookItemId = table.Column<long>(type: "bigint", nullable: false),
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReadyAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReadyExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_ItemReservation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationChannelConfig",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SmsAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SmsSender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SmsApiUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZaloAccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ZaloApiUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_NotificationChannelConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLog",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Recipient = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EventCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderBadge",
                schema: "Gamification",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    BadgeId = table.Column<long>(type: "bigint", nullable: false),
                    EarnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderBadge", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderPhoto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    PhotoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderPhoto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReaderPreference",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    SubjectId = table.Column<long>(type: "bigint", nullable: true),
                    TopicId = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderPreference", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomBooking",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MapObjectId = table.Column<long>(type: "bigint", nullable: false),
                    ReaderId = table.Column<long>(type: "bigint", nullable: false),
                    StartAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PartySize = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedBy = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_RoomBooking", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomBookingConfig",
                schema: "map",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MapObjectId = table.Column<long>(type: "bigint", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false),
                    SlotMinutes = table.Column<int>(type: "int", nullable: false),
                    MinAdvanceMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxAdvanceDays = table.Column<int>(type: "int", nullable: false),
                    MaxBookingMinutesPerReader = table.Column<int>(type: "int", nullable: false),
                    CheckInGraceMinutes = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_RoomBookingConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledReport",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReportParamsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrequencyType = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    TimeOfDay = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecipientEmails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunStatus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDelete = table.Column<int>(type: "int", nullable: true),
                    CreatedRowBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdateRowBy = table.Column<long>(type: "bigint", nullable: true),
                    CreatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedRowDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "newsequentialid()"),
                    TenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReport", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Badge",
                schema: "Gamification");

            migrationBuilder.DropTable(
                name: "DocumentSubmission",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "ItemReservation",
                schema: "Ebook");

            migrationBuilder.DropTable(
                name: "NotificationChannelConfig",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NotificationLog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReaderBadge",
                schema: "Gamification");

            migrationBuilder.DropTable(
                name: "ReaderPhoto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ReaderPreference",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RoomBooking",
                schema: "map");

            migrationBuilder.DropTable(
                name: "RoomBookingConfig",
                schema: "map");

            migrationBuilder.DropTable(
                name: "ScheduledReport",
                schema: "dbo");

            migrationBuilder.AlterColumn<int>(
                name: "DocGroupId",
                schema: "PrintBook",
                table: "PolicyCircDocGroup",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
