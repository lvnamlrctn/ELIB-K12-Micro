namespace ELIBAPI.Infrastructure.Data;

/// <summary>DDL bổ sung cho đặt phòng (PostgreSQL) — chạy lúc khởi động trong Program.cs và giống hệt
/// scripts/room-booking-rules-postgresql.sql. Chỉ thêm cột/bảng, IF NOT EXISTS nên chạy lại an toàn.
/// Port ELIB-LRC 10-04 (đợt A–D). Tenant (K12): mọi bảng mới có cột "TenantId" + index theo đơn vị; UID thẻ chỉ duy nhất
/// trong 1 đơn vị (bạn đọc, thẻ quản trị); mã thiết bị duy nhất toàn hệ thống (thiết bị tự xác thực bằng mã + khoá).</summary>
public static class RoomBookingSchema
{
    public const string PostgreSql = @"
        CREATE SCHEMA IF NOT EXISTS map;
        ALTER TABLE IF EXISTS map.""RoomBooking"" ADD COLUMN IF NOT EXISTS ""CheckedOutAt"" timestamp without time zone NULL;
        ALTER TABLE IF EXISTS map.""RoomBookingConfig"" ADD COLUMN IF NOT EXISTS ""AutoApprove"" boolean NOT NULL DEFAULT false;
        ALTER TABLE IF EXISTS map.""RoomBookingConfig"" ADD COLUMN IF NOT EXISTS ""MaxAdvanceHours"" integer NULL;
        ALTER TABLE IF EXISTS map.""RoomBookingConfig"" ADD COLUMN IF NOT EXISTS ""Rules"" text NULL;
        ALTER TABLE IF EXISTS map.""RoomBookingConfig"" ADD COLUMN IF NOT EXISTS ""AllowedReaderTypeIds"" character varying(500) NULL;

        CREATE TABLE IF NOT EXISTS map.""RoomBookingBan"" (
            ""Id""             bigserial PRIMARY KEY,
            ""ReaderId""       bigint NOT NULL,
            ""Reason""         character varying(500) NULL,
            ""Source""         integer NOT NULL DEFAULT 2,
            ""BannedUntil""    timestamp without time zone NOT NULL,
            ""LiftedAt""       timestamp without time zone NULL,
            ""LiftedBy""       bigint NULL,
            ""CreatedRowBy""   bigint NULL,
            ""CreatedRowDate"" timestamp without time zone NULL,
            ""TenantId""       bigint NULL,
            ""PublicId""       uuid NOT NULL DEFAULT gen_random_uuid()
        );
        CREATE INDEX IF NOT EXISTS ""IX_RoomBookingBan_ReaderId"" ON map.""RoomBookingBan"" (""ReaderId"");
        CREATE INDEX IF NOT EXISTS ""IX_RoomBookingBan_TenantId"" ON map.""RoomBookingBan"" (""TenantId"");

        CREATE TABLE IF NOT EXISTS map.""RoomOpeningHour"" (
            ""Id""             bigserial PRIMARY KEY,
            ""Category""       integer NULL,
            ""Weekday""        integer NOT NULL,
            ""OpenTime""       character varying(5) NULL,
            ""CloseTime""      character varying(5) NULL,
            ""IsClosed""       boolean NOT NULL DEFAULT false,
            ""TenantId""       bigint NULL,
            ""UpdateRowBy""    bigint NULL,
            ""UpdatedRowDate"" timestamp without time zone NULL
        );

        CREATE TABLE IF NOT EXISTS map.""RoomSpecialDay"" (
            ""Id""             bigserial PRIMARY KEY,
            ""Date""           timestamp without time zone NOT NULL,
            ""Category""       integer NULL,
            ""IsClosed""       boolean NOT NULL DEFAULT true,
            ""OpenTime""       character varying(5) NULL,
            ""CloseTime""      character varying(5) NULL,
            ""Reason""         character varying(500) NULL,
            ""CreatedRowBy""   bigint NULL,
            ""CreatedRowDate"" timestamp without time zone NULL,
            ""TenantId""       bigint NULL,
            ""PublicId""       uuid NOT NULL DEFAULT gen_random_uuid()
        );
        CREATE INDEX IF NOT EXISTS ""IX_RoomSpecialDay_TenantId_Date"" ON map.""RoomSpecialDay"" (""TenantId"", ""Date"");
        CREATE INDEX IF NOT EXISTS ""IX_RoomOpeningHour_TenantId"" ON map.""RoomOpeningHour"" (""TenantId"", ""Weekday"");

        -- Đợt C+D: UID thẻ chip, đặt theo nhóm, tạm ngưng phòng, kiểm soát cửa.
        ALTER TABLE IF EXISTS public.""Reader"" ADD COLUMN IF NOT EXISTS ""CardUid"" character varying(64) NULL;
        CREATE INDEX IF NOT EXISTS ""IX_Reader_TenantId_CardUid"" ON public.""Reader"" (""TenantId"", ""CardUid"") WHERE ""CardUid"" IS NOT NULL;
        ALTER TABLE IF EXISTS map.""RoomBookingConfig"" ADD COLUMN IF NOT EXISTS ""MinGroupSize"" integer NULL;
        ALTER TABLE IF EXISTS map.""RoomBookingConfig"" ADD COLUMN IF NOT EXISTS ""Maintenance"" boolean NOT NULL DEFAULT false;
        ALTER TABLE IF EXISTS map.""RoomBookingConfig"" ADD COLUMN IF NOT EXISTS ""MaintenanceNote"" character varying(500) NULL;

        CREATE TABLE IF NOT EXISTS map.""RoomBookingMember"" (
            ""Id""             bigserial PRIMARY KEY,
            ""BookingId""      bigint NOT NULL,
            ""ReaderId""       bigint NOT NULL,
            ""CreatedRowDate"" timestamp without time zone NULL,
            ""TenantId""       bigint NULL
        );
        CREATE INDEX IF NOT EXISTS ""IX_RoomBookingMember_BookingId"" ON map.""RoomBookingMember"" (""BookingId"");
        CREATE INDEX IF NOT EXISTS ""IX_RoomBookingMember_ReaderId"" ON map.""RoomBookingMember"" (""ReaderId"");

        CREATE TABLE IF NOT EXISTS map.""AccessDevice"" (
            ""Id""             bigserial PRIMARY KEY,
            ""Code""           character varying(100) NOT NULL,
            ""Name""           character varying(200) NULL,
            ""MapObjectId""    bigint NULL,
            ""ApiKeyHash""     character varying(128) NOT NULL,
            ""IsActive""       boolean NOT NULL DEFAULT true,
            ""LastSeenAt""     timestamp without time zone NULL,
            ""CreatedRowBy""   bigint NULL,
            ""CreatedRowDate"" timestamp without time zone NULL,
            ""UpdatedRowDate"" timestamp without time zone NULL,
            ""TenantId""       bigint NULL,
            ""PublicId""       uuid NOT NULL DEFAULT gen_random_uuid()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_AccessDevice_Code"" ON map.""AccessDevice"" (""Code"");
        CREATE INDEX IF NOT EXISTS ""IX_AccessDevice_TenantId"" ON map.""AccessDevice"" (""TenantId"");

        CREATE TABLE IF NOT EXISTS map.""AccessStaffCard"" (
            ""Id""             bigserial PRIMARY KEY,
            ""CardUid""        character varying(64) NOT NULL,
            ""UserId""         bigint NULL,
            ""Label""          character varying(200) NULL,
            ""IsActive""       boolean NOT NULL DEFAULT true,
            ""CreatedRowBy""   bigint NULL,
            ""CreatedRowDate"" timestamp without time zone NULL,
            ""TenantId""       bigint NULL,
            ""PublicId""       uuid NOT NULL DEFAULT gen_random_uuid()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_AccessStaffCard_Tenant_CardUid"" ON map.""AccessStaffCard"" (COALESCE(""TenantId"", 0), ""CardUid"");

        CREATE TABLE IF NOT EXISTS map.""AccessScanLog"" (
            ""Id""             bigserial PRIMARY KEY,
            ""DeviceId""       bigint NULL,
            ""DeviceCode""     character varying(100) NULL,
            ""MapObjectId""    bigint NULL,
            ""CardUid""        character varying(64) NULL,
            ""ReaderId""       bigint NULL,
            ""StaffUserId""    bigint NULL,
            ""BookingId""      bigint NULL,
            ""Source""         integer NOT NULL,
            ""Allowed""        boolean NOT NULL,
            ""Reason""         character varying(500) NULL,
            ""ScannedAt""      timestamp without time zone NULL,
            ""CreatedAt""      timestamp without time zone NOT NULL,
            ""TenantId""       bigint NULL
        );
        CREATE INDEX IF NOT EXISTS ""IX_AccessScanLog_TenantId_CreatedAt"" ON map.""AccessScanLog"" (""TenantId"", ""CreatedAt"");

        CREATE TABLE IF NOT EXISTS map.""AccessCommand"" (
            ""Id""             bigserial PRIMARY KEY,
            ""DeviceId""       bigint NOT NULL,
            ""Command""        character varying(20) NOT NULL,
            ""RequestedBy""    bigint NULL,
            ""BookingId""      bigint NULL,
            ""Note""           character varying(500) NULL,
            ""RequestedAt""    timestamp without time zone NOT NULL,
            ""ExpiresAt""      timestamp without time zone NOT NULL,
            ""DeliveredAt""    timestamp without time zone NULL,
            ""AckAt""          timestamp without time zone NULL,
            ""TenantId""       bigint NULL,
            ""PublicId""       uuid NOT NULL DEFAULT gen_random_uuid()
        );
        CREATE INDEX IF NOT EXISTS ""IX_AccessCommand_DeviceId"" ON map.""AccessCommand"" (""DeviceId"", ""DeliveredAt"");
    ";
}
