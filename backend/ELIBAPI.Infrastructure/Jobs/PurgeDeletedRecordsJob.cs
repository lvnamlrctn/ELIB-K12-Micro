using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ELIBAPI.Infrastructure.Jobs;

public class PurgeDeletedRecordsJob(ELIBAPIDbContext db, IConfiguration config)
{
    public async Task RunAsync()
    {
        var isPostgres = (config["DatabaseProvider"] ?? "")
            .Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase);

        if (isPostgres)
            await PurgePostgresAsync();
        else
            await PurgeSqlServerAsync();
    }

    private async Task PurgePostgresAsync()
    {
        // Tìm tất cả bảng có cột IsDelete + UpdatedRowDate, xóa bản ghi IsDelete=2 quá 30 ngày
        var sql = @"
DO $$
DECLARE r RECORD; stmt TEXT;
BEGIN
  FOR r IN
    SELECT DISTINCT c1.table_schema, c1.table_name
    FROM information_schema.columns c1
    JOIN information_schema.columns c2
      ON  c2.table_schema = c1.table_schema
      AND c2.table_name   = c1.table_name
      AND c2.column_name  = 'UpdatedRowDate'
    WHERE c1.column_name = 'IsDelete'
      AND c1.table_schema NOT IN ('pg_catalog','information_schema','hangfire')
  LOOP
    BEGIN
      stmt := format(
        'DELETE FROM %I.%I WHERE ""IsDelete"" = 2 AND ""UpdatedRowDate"" < NOW() - INTERVAL ''30 days''',
        r.table_schema, r.table_name);
      EXECUTE stmt;
    EXCEPTION WHEN OTHERS THEN NULL;
    END;
  END LOOP;
END $$;";
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task PurgeSqlServerAsync()
    {
        var sql = @"
DECLARE @sql NVARCHAR(MAX) = '';
SELECT @sql += 'BEGIN TRY DELETE FROM ' + QUOTENAME(c1.TABLE_SCHEMA) + '.' + QUOTENAME(c1.TABLE_NAME) +
  ' WHERE IsDelete = 2 AND UpdatedRowDate < DATEADD(DAY,-30,GETDATE()); END TRY BEGIN CATCH END CATCH;' + CHAR(10)
FROM INFORMATION_SCHEMA.COLUMNS c1
WHERE c1.COLUMN_NAME = 'IsDelete'
  AND EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS c2
    WHERE c2.TABLE_SCHEMA = c1.TABLE_SCHEMA
      AND c2.TABLE_NAME   = c1.TABLE_NAME
      AND c2.COLUMN_NAME  = 'UpdatedRowDate');
EXEC sp_executesql @sql;";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}
