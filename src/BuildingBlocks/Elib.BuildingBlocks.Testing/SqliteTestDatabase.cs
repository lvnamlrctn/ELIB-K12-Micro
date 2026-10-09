using Microsoft.Data.Sqlite;

namespace Elib.BuildingBlocks.Testing;

/// <summary>
/// DB SQLite dạng file tạm, chế độ WAL — mỗi DbContext tự mở connection riêng như với PostgreSQL thật.
/// KHÔNG dùng một SqliteConnection in-memory chung: consumer của bus chạy song song với request test,
/// và SqliteConnection không an toàn đa luồng (lỗi chập chờn "unable to delete/modify user-function due to active statements").
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"elib-test-{Guid.NewGuid():N}.db");

    public SqliteTestDatabase()
    {
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            DefaultTimeout = 30,
            Pooling = false,
        }.ToString();

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();
    }

    public string ConnectionString { get; }

    public void Dispose()
    {
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // File tạm — hệ điều hành dọn sau nếu còn bị giữ.
            }
        }
    }
}
