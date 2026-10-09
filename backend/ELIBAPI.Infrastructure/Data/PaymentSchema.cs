namespace ELIBAPI.Infrastructure.Data;

/// <summary>DDL thanh toán QR (PostgreSQL) — chạy lúc khởi động trong Program.cs, giống hệt scripts/payment-postgresql.sql.
/// Port ELIB-LRC 09-25 (migration AddPaymentTransaction) + cột TenantId (K12). IF NOT EXISTS nên chạy lại an toàn.</summary>
public static class PaymentSchema
{
    public const string PostgreSql = @"
        CREATE SCHEMA IF NOT EXISTS payment;
        CREATE TABLE IF NOT EXISTS payment.""PaymentTransaction"" (
            ""Id""              bigserial PRIMARY KEY,
            ""TransactionCode"" character varying(40) NOT NULL,
            ""ReaderId""        bigint NOT NULL,
            ""TargetType""      character varying(30) NOT NULL,
            ""TargetId""        bigint NULL,
            ""Amount""          double precision NOT NULL,
            ""Provider""        character varying(20) NOT NULL,
            ""Status""          character varying(20) NOT NULL DEFAULT 'Pending',
            ""QrContent""       text NULL,
            ""ProviderRef""     character varying(100) NULL,
            ""ExpiresAt""       timestamp without time zone NOT NULL,
            ""PaidAt""          timestamp without time zone NULL,
            ""IsDelete""        integer NULL,
            ""CreatedRowBy""    bigint NULL,
            ""UpdateRowBy""     bigint NULL,
            ""CreatedRowDate""  timestamp without time zone NULL,
            ""UpdatedRowDate""  timestamp without time zone NULL,
            ""TenantId""        bigint NULL,
            ""PublicId""        uuid NOT NULL DEFAULT gen_random_uuid()
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PaymentTransaction_TransactionCode"" ON payment.""PaymentTransaction"" (""TransactionCode"");
        CREATE INDEX IF NOT EXISTS ""IX_PaymentTransaction_Status_ExpiresAt"" ON payment.""PaymentTransaction"" (""Status"", ""ExpiresAt"");
        CREATE INDEX IF NOT EXISTS ""IX_PaymentTransaction_Target"" ON payment.""PaymentTransaction"" (""TargetType"", ""TargetId"");
        CREATE INDEX IF NOT EXISTS ""IX_PaymentTransaction_ReaderId"" ON payment.""PaymentTransaction"" (""ReaderId"");
        CREATE INDEX IF NOT EXISTS ""IX_PaymentTransaction_TenantId"" ON payment.""PaymentTransaction"" (""TenantId"");
";
}
