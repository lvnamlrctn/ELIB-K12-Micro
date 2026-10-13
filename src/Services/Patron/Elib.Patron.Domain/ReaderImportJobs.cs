using Elib.BuildingBlocks.Domain;

namespace Elib.Patron.Domain;

/// <summary>Ready: đã xem trước, chờ bấm chạy; Queued: đã bấm chạy; Running; Done; Failed (lỗi cả lượt, không phải lỗi từng dòng).</summary>
public enum ReaderImportStatus
{
    Ready = 1,
    Queued = 2,
    Running = 3,
    Done = 4,
    Failed = 5,
}

/// <summary>Import: nhập Excel; BatchUpdate: sửa hàng loạt theo bộ lọc khớp nhiều hơn số bạn đọc sửa ngay trong request.</summary>
public enum ReaderJobKind
{
    Import = 1,
    BatchUpdate = 2,
}

/// <summary>
/// Một lượt nhập bạn đọc từ Excel chạy nền (ELIB-LRC: Reader/Import với previewOnly/previewToken/background). Giữ file gốc và tuỳ chọn
/// để chạy lại đúng như bản xem trước; cũng dùng cho sửa hàng loạt chạy nền (<see cref="ReaderJobKind.BatchUpdate"/>, không có file); file "Dòng cần sửa" (xlsx) cho người dùng tải về sửa rồi nhập lại. Dọn sau <see cref="Retention"/>.
/// </summary>
public sealed class ReaderImportJob : TenantEntity
{
    public const int MaxFileBytes = 20 * 1024 * 1024;
    public const int MaxRows = 50_000;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private ReaderImportJob() { }

    public static ReaderImportJob Prepare(string fileName, byte[] file, string optionsJson, int total, int toCreate, int toUpdate,
        int errorCount, string errorsJson, byte[]? errorFile)
    {
        if (file.Length is 0 or > MaxFileBytes)
            throw new BusinessRuleException("IMPORT_FILE_SIZE", $"File rỗng hoặc lớn hơn {MaxFileBytes / 1024 / 1024} MB.");
        return new ReaderImportJob
        {
            Status = ReaderImportStatus.Ready,
            FileName = Cut(string.IsNullOrWhiteSpace(fileName) ? "readers.xlsx" : Path.GetFileName(fileName), 255),
            File = file,
            OptionsJson = optionsJson,
            Total = total,
            ToCreate = toCreate,
            ToUpdate = toUpdate,
            ErrorCount = errorCount,
            ErrorsJson = errorsJson,
            ErrorFile = errorFile,
        };
    }

    /// <summary>Sửa hàng loạt chạy nền: vào hàng đợi ngay (đã xem trước + xác nhận ở request). <paramref name="optionsJson"/>: yêu cầu sửa.</summary>
    public static ReaderImportJob QueueBatchUpdate(string label, string optionsJson, int total, string originJson) => new()
    {
        Kind = ReaderJobKind.BatchUpdate,
        Status = ReaderImportStatus.Queued,
        FileName = Cut(label, 255),
        OptionsJson = optionsJson,
        Total = total,
        ToUpdate = total,
        OriginJson = originJson,
    };

    public ReaderJobKind Kind { get; private set; } = ReaderJobKind.Import;

    /// <summary>Người bấm chạy (id, tên, lý do sửa, IP) — worker ghi nhật ký/event dưới tên người này.</summary>
    public string? OriginJson { get; private set; }

    public ReaderImportStatus Status { get; private set; }
    public string FileName { get; private set; } = "";
    public byte[] File { get; private set; } = [];
    public string OptionsJson { get; private set; } = "{}";

    /// <summary>Kết quả xem trước.</summary>
    public int Total { get; private set; }
    public int ToCreate { get; private set; }
    public int ToUpdate { get; private set; }
    public int ErrorCount { get; private set; }

    /// <summary>Tối đa 200 lỗi đầu (số dòng + thông báo) dạng JSON — hiện ngay trên hộp thoại.</summary>
    public string ErrorsJson { get; private set; } = "[]";

    /// <summary>Kết quả chạy.</summary>
    public int Processed { get; private set; }
    public int Created { get; private set; }
    public int Updated { get; private set; }
    public int Failed { get; private set; }

    /// <summary>File "Dòng cần sửa": các dòng lỗi (cột gốc + cột Lỗi).</summary>
    public byte[]? ErrorFile { get; private set; }
    public string? Message { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public void Queue(string originJson)
    {
        if (Status != ReaderImportStatus.Ready)
            throw new ConflictException("IMPORT_JOB_STATE", "Lượt nhập này đã chạy hoặc đang chạy.");
        Status = ReaderImportStatus.Queued;
        OriginJson = originJson;
    }

    public void Start(DateTimeOffset now)
    {
        Status = ReaderImportStatus.Running;
        StartedAt = now;
        Processed = Created = Updated = Failed = 0;
    }

    public void Progress(int processed, int created, int updated, int failed)
    {
        Processed = processed;
        Created = created;
        Updated = updated;
        Failed = failed;
    }

    public void Finish(DateTimeOffset now, byte[]? errorFile, string errorsJson)
    {
        Status = ReaderImportStatus.Done;
        FinishedAt = now;
        ErrorFile = errorFile;
        ErrorsJson = errorsJson;
        File = []; // đã nhập xong — không giữ dữ liệu cá nhân trong file gốc lâu hơn cần
    }

    public void Fail(DateTimeOffset now, string message)
    {
        Status = ReaderImportStatus.Failed;
        FinishedAt = now;
        Message = Cut(message, 1000);
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
