using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELIBAPI.Core.DTOs.Request;
using ELIBAPI.Core.Entities.Dbo;
using ELIBAPI.Core.Interfaces;
using ELIBAPI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Nền tảng tác vụ nền cho thao tác hàng loạt tốn thời gian (Đợt 10 — port từ ELIB-LRC
/// "AdminTask v2"). 3 kind: "reader-import" (nhập/cập nhật độc giả), "barcode-reregister-import"/
/// "inventory-import" (Đợt 22.5 — đánh lại mã ĐKCB/nhập kiểm kê hàng loạt qua Excel). Phần
/// claim/chunk/worker nằm ở <c>AdminTaskChunks.cs</c> (cùng partial class).</summary>
public partial class AdminTaskService(
    ELIBAPIDbContext db,
    AdminTaskCrypto crypto,
    IReaderRepository readers,
    IPermissionService permissions,
    BarcodeReRegisterService barcodeReRegister,
    InventoryImportService inventoryImport)
{
    public const int ChunkSize = 200;
    public const int MaxRows = 50_000;
    internal static readonly JsonSerializerOptions Json = new();

    public static string Module(string kind) => kind switch
    {
        "reader-import" => "READERS",
        "barcode-reregister-import" => "RE_REGISTER",
        "inventory-import" => "INVENTORY",
        _ => throw new InvalidOperationException($"Loại tác vụ '{kind}' không hợp lệ."),
    };

    /// <summary>Kiểm tra quyền — gọi lại ở MỖI chunk (không chỉ lúc enqueue), phòng trường hợp quyền bị
    /// thu hồi giữa chừng khi tác vụ còn nằm trong hàng đợi.</summary>
    public async Task Authorize(long actorId, AdminTaskRequest request)
    {
        var action = request.Kind switch
        {
            "reader-import" => "add",
            "inventory-import" => "add",
            _ => "edit", // barcode-reregister-import đúng bằng quyền RE_REGISTER,edit của bản đơn lẻ
        };
        var ok = actorId > 0 && await permissions.HasPermissionAsync(actorId, Module(request.Kind), action);
        if (ok && request.Kind == "reader-import" && request.Overwrite)
            ok = await permissions.HasPermissionAsync(actorId, "READERS", "edit");
        if (!ok) throw new UnauthorizedAccessException("Không có quyền thực hiện tác vụ này.");
    }

    private async Task<string?> ActorNameAsync(long actorId)
        => await db.Users.AsNoTracking().Where(u => u.Id == actorId).Select(u => u.FullName ?? u.LoginName).FirstOrDefaultAsync();

    /// <summary>Số dòng của request theo đúng kind — dùng để kiểm tra <see cref="MaxRows"/> và tính chunk.</summary>
    private static int RowCount(AdminTaskRequest r) => r.Kind switch
    {
        "reader-import" => r.Rows?.Count ?? 0,
        "barcode-reregister-import" => r.BarcodeRows?.Count ?? 0,
        "inventory-import" => r.InventoryRows?.Count ?? 0,
        _ => 0,
    };

    /// <summary>Chạy toàn bộ (không chunk) logic xem trước của 1 kind — dùng để (1) sinh Result/ReviewToken
    /// khi 1 tác vụ Preview hoàn tất tất cả chunk (<c>Finish</c> đọc lại từ Result của từng chunk, KHÔNG gọi
    /// hàm này) và (2) xác thực lại chữ ký ngay lúc xác nhận (<see cref="Enqueue"/>, luôn previewOnly=true dù
    /// tác vụ xác nhận sẽ ghi thật — bản ghi thật diễn ra sau, khi <c>RunNext</c> xử lý tác vụ Preview=false).</summary>
    private async Task<ReaderImportResult> RunKindPreview(AdminTaskRequest request, long actorId, long? tenantId) => request.Kind switch
    {
        "reader-import" => await readers.ImportAsync(request.Rows ?? new List<ReaderImportRow>(), request.PortalId, request.Language,
            request.Overwrite, request.ReaderTypeId, request.ClassByCode, request.CourseByCode, request.OrgByCode,
            previewOnly: true, autoCreateRefs: request.AutoCreateRefs),
        "barcode-reregister-import" => await ExecuteBarcodeRows(request.BarcodeRows ?? new List<BarcodeReRegisterRow>(), tenantId, actorId, previewOnly: true),
        "inventory-import" => await ExecuteInventoryRows(request, tenantId, actorId, previewOnly: true),
        _ => throw new InvalidOperationException($"Loại tác vụ '{request.Kind}' chưa hỗ trợ."),
    };

    public static object View(AdminTask task)
    {
        object? result = null;
        if (!string.IsNullOrEmpty(task.Result))
        {
            try { result = JsonSerializer.Deserialize<JsonElement>(task.Result); }
            catch { /* Result đã bị dọn hoặc lỗi định dạng — bỏ qua, vẫn trả các field khác */ }
        }
        return new
        {
            id = task.Id,
            kind = task.Kind,
            state = task.State,
            preview = task.Preview,
            totalChunks = task.TotalChunks,
            completedChunks = task.CompletedChunks,
            totalItems = task.TotalItems,
            completedItems = task.CompletedItems,
            error = task.Error,
            reviewToken = task.ReviewToken,
            createdAt = task.CreatedAt,
            updatedAt = task.UpdatedAt,
            result,
        };
    }

    /// <summary>Phần tóm tắt request được đưa vào chữ ký HMAC — không dùng nguyên object AdminTaskRequest
    /// (Token có thể null lúc ký, khác lúc xác minh) mà chỉ trích các field thật sự xác định "nội dung
    /// thao tác là gì".</summary>
    private static object RequestSummary(AdminTaskRequest r) => new
    {
        r.Kind,
        cardnos = r.Rows?.Select(x => x.Cardno) ?? Enumerable.Empty<string?>(),
        r.PortalId,
        r.Language,
        r.Overwrite,
        r.ReaderTypeId,
        r.ClassByCode,
        r.CourseByCode,
        r.OrgByCode,
        r.AutoCreateRefs,
        barcodeRows = r.BarcodeRows?.Select(x => new { x.OldBarcode, x.NewBarcode, x.StoreId }) ?? Enumerable.Empty<object>(),
        r.InventoryId,
        inventoryRows = r.InventoryRows?.Select(x => new { x.Barcode, x.StoreId }) ?? Enumerable.Empty<object>(),
    };

    private static Guid DeriveConfirmId(Guid previewOperationId)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes("admin-task-confirm-v1:" + previewOperationId))[..16]);

    /// <summary>Tạo tác vụ mới. <paramref name="request"/>.Preview=true → tác vụ xem trước (không ghi gì);
    /// Preview=false → tác vụ xác nhận, bắt buộc <paramref name="request"/>.Token hợp lệ của 1 tác vụ xem
    /// trước đã hoàn tất — được xác thực lại 1 lần ngay tại đây (chạy 1 lượt xem-trước-lại đầy đủ, so khớp
    /// chữ ký) trước khi cho enqueue tác vụ ghi thật.</summary>
    public async Task<AdminTask> Enqueue(long actorId, long? tenantId, AdminTaskRequest request)
    {
        await Authorize(actorId, request);

        if (request.Preview)
        {
            if (RowCount(request) > MaxRows)
                throw new InvalidOperationException($"Vượt giới hạn {MaxRows} dòng cho 1 tác vụ.");

            var task = new AdminTask
            {
                Id = Guid.NewGuid(),
                ActorId = actorId,
                TenantId = tenantId,
                Kind = request.Kind,
                State = "Queued",
                Preview = true,
                Payload = crypto.Protect(JsonSerializer.Serialize(request, Json)),
                Attempts = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Version = 2,
            };
            db.AdminTasks.Add(task);
            await db.SaveChangesAsync();
            return task;
        }

        if (string.IsNullOrEmpty(request.Token))
            throw new InvalidOperationException("Cần xem trước trước khi xác nhận.");
        var (expires, operationId) = AdminMutationGuard.ParseToken(request.Token);

        var preview = await db.AdminTasks.FindAsync(operationId)
            ?? throw new InvalidOperationException("Không tìm thấy phiên xem trước tương ứng.");
        if (preview.ReviewToken != request.Token)
            throw new InvalidOperationException("Token xem trước không hợp lệ.");
        if (preview.State != "Completed")
            throw new InvalidOperationException("Phiên xem trước chưa hoàn tất.");

        var previewRequest = JsonSerializer.Deserialize<AdminTaskRequest>(crypto.Unprotect(preview.Payload!), Json)!;

        // Xác thực lại NGAY tại đây (1 lần, đủ dữ liệu để đối chiếu): chạy lại toàn bộ logic xem trước
        // trên chính request gốc đã lưu, so khớp chữ ký — dữ liệu bị ai đó sửa từ lúc xem trước sẽ làm
        // lệch kết quả tính lại và bị chặn.
        var freshResult = await RunKindPreview(previewRequest, actorId, tenantId);
        AdminMutationGuard.Verify(RequestSummary(previewRequest), freshResult.Review?.Changes ?? new List<ReaderChangePreview>(),
            request.Token, skipExpiry: false);

        var confirmTask = new AdminTask
        {
            // Id suy ra tất định từ operationId (KHÔNG trùng preview.Id) — xác nhận 2 lần cùng token
            // (double-click, request lặp) luôn ra cùng 1 Id, INSERT lần 2 vi phạm PK → bắt được, trả lại
            // đúng tác vụ đã tạo thay vì tạo trùng.
            Id = DeriveConfirmId(operationId),
            ActorId = actorId,
            TenantId = tenantId,
            Kind = request.Kind,
            State = "Queued",
            Preview = false,
            Payload = crypto.Protect(JsonSerializer.Serialize(previewRequest, Json)),
            Attempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Version = 2,
            SourcePreviewId = preview.Id,
        };
        db.AdminTasks.Add(confirmTask);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return await db.AdminTasks.AsNoTracking().FirstAsync(x => x.Id == confirmTask.Id);
        }
        return confirmTask;
    }

    /// <summary>File Excel "Dòng cần sửa" của 1 tác vụ nhập bạn đọc (Đợt 20, port ELIB-LRC): dòng gốc lấy từ payload
    /// đã mã hoá của tác vụ, lỗi lấy từ kết quả (xem trước hoặc ghi thật). Chỉ tác vụ của chính người gọi.
    /// null = không tìm thấy / không phải tác vụ nhập / dữ liệu đã bị dọn theo chính sách lưu trữ.</summary>
    public async Task<byte[]?> ImportErrorsWorkbook(long actorId, Guid id)
    {
        var task = await db.AdminTasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ActorId == actorId);
        if (task == null || task.Kind != "reader-import" || string.IsNullOrEmpty(task.Payload) || string.IsNullOrEmpty(task.Result))
            return null;

        var request = JsonSerializer.Deserialize<AdminTaskRequest>(crypto.Unprotect(task.Payload), Json)!;
        var errors = new List<string>();
        using (var doc = JsonDocument.Parse(task.Result))
        {
            foreach (var prop in doc.RootElement.EnumerateObject())
                if (prop.Name.Equals("errors", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.Array)
                    errors.AddRange(prop.Value.EnumerateArray().Select(e => e.GetString() ?? ""));
        }
        return ReaderImportWorkbook.Errors(request.Rows ?? new List<ReaderImportRow>(), errors);
    }
}
