# Hướng dẫn copy dữ liệu từ SQL Server cũ sang SQL Server Docker

## Cách 1: Backup/Restore (khuyến nghị)

### Bước 1: Backup database từ SQL Server cũ

```sql
-- Trên SQL Server cũ (103.97.134.58,1447)
BACKUP DATABASE [ELIBAPI] 
TO DISK = '/var/opt/mssql/backup/ELIBAPI.bak' 
WITH FORMAT, COMPRESSION;
```

Hoặc dùng `sqlcmd`:
```bash
sqlcmd -S 103.97.134.58,1447 -U sa -P 'YOUR_PASSWORD' \
  -Q "BACKUP DATABASE [ELIBAPI] TO DISK='/var/opt/mssql/backup/ELIBAPI.bak' WITH FORMAT, COMPRESSION"
```

### Bước 2: Copy file .bak vào Docker container

```bash
# Copy file backup vào container
docker cp ELIBAPI.bak elibapi-mssql:/var/opt/mssql/backup/

# Hoặc mount thư mục backup
# Thêm volume vào docker-compose.yml:
#   - ./backup:/var/opt/mssql/backup
```

### Bước 3: Restore trong Docker container

```bash
docker exec -it elibapi-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStr0ng!Pass' -C \
  -Q "RESTORE DATABASE [ELIBAPI] FROM DISK='/var/opt/mssql/backup/ELIBAPI.bak' WITH REPLACE"
```

## Cách 2: Dùng EF Core auto-migrate (database trống)

Nếu không cần dữ liệu cũ, chỉ cần khởi động app:

```bash
docker compose up -d
```

App sẽ tự chạy `db.Database.Migrate()` khi khởi động, tạo toàn bộ schema từ EF Core migration.

Sau đó chạy seed SQL:
```bash
# Seed modules
docker exec -i elibapi-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'YourStr0ng!Pass' -d ELIBAPI -C \
  -i /tmp/seed.sql

# Copy seed file vào container trước
docker cp docs/run-module-seed.sql elibapi-mssql:/tmp/seed.sql
```

## Cách 3: BCP (Bulk Copy Program)

Export từng bảng từ SQL cũ, import vào SQL Docker:

```bash
# Export
bcp "dbo.Users" out users.dat -S 103.97.134.58,1447 -U sa -P 'OLD_PASS' -N

# Import
bcp "dbo.Users" in users.dat -S localhost,1433 -U sa -P 'YourStr0ng!Pass' -N -d ELIBAPI
```

## Lưu ý

- Sau khi restore, không cần chạy EF Core migration vì database đã có schema đầy đủ
- Nếu dùng auto-migrate trước, rồi restore sau, cần `WITH REPLACE` để ghi đè
- Password SQL Server Docker phải đủ mạnh (có chữ hoa, thường, số, ký tự đặc biệt)
