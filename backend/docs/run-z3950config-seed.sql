/* ==========================================================================
   SEED PrintBook.Z3950Config — Systax = 'Internal' cho 94 don vi
   Nguon: docs/run-tenant-seed.sql (chay truoc de co Tenant)
   CANH BAO: Xoa toan bo Z3950Config cu. Backup DB truoc khi chay.
   ========================================================================== */
SET NOCOUNT ON;

DELETE FROM [PrintBook].[Z3950Config];
DBCC CHECKIDENT ('[PrintBook].[Z3950Config]', RESEED, 0);

INSERT INTO [PrintBook].[Z3950Config]
    (PublicId, Name, Systax, TenantId, IsDelete, CreatedRowDate)
SELECT
    NEWID(),
    N'Thư viện ' + t.Name,
    N'Internal',
    t.Id,
    1,
    GETDATE()
FROM [dbo].[Tenant] t;

SELECT COUNT(*) AS [Z3950Config Systax=Internal]
FROM [PrintBook].[Z3950Config]
WHERE Systax = N'Internal';
