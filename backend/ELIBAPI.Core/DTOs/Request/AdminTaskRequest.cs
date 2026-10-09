namespace ELIBAPI.Core.DTOs.Request;

/// <summary>Nội dung 1 tác vụ nền (Đợt 10) — mã hoá trước khi lưu vào <c>AdminTask.Payload</c>/
/// <c>AdminTaskChunk.Payload</c> (xem <c>AdminTaskCrypto</c>). <see cref="Kind"/> chọn field nào dưới đây có
/// dữ liệu — dùng union phẳng thay vì kế thừa (Đợt 22.5 thêm "barcode-reregister-import"/"inventory-import"
/// bên cạnh "reader-import" gốc: chỉ 3 kind, mỗi kind vài field, không đáng thiết kế lại DTO).</summary>
public class AdminTaskRequest
{
    public string Kind { get; set; } = "";
    /// <summary>true = xem trước (không ghi), false = xác nhận (bắt buộc có <see cref="Token"/> hợp lệ).</summary>
    public bool Preview { get; set; }
    /// <summary>Bắt buộc khi <see cref="Preview"/>=false — token của tác vụ xem trước tương ứng
    /// (<c>AdminTask.ReviewToken</c>).</summary>
    public string? Token { get; set; }

    // ── reader-import ────────────────────────────────────────────────────────
    public List<ReaderImportRow>? Rows { get; set; }
    public string? PortalId { get; set; }
    public string? Language { get; set; }
    public bool Overwrite { get; set; }
    public long? ReaderTypeId { get; set; }
    public bool ClassByCode { get; set; }
    public bool CourseByCode { get; set; }
    public bool OrgByCode { get; set; }
    /// <summary>Tự tạo Lớp/Khóa học/Đơn vị chưa có (chỉ khi tra theo tên) — Đợt 20.</summary>
    public bool AutoCreateRefs { get; set; }

    // ── barcode-reregister-import (Đợt 22.5) ────────────────────────────────
    public List<BarcodeReRegisterRow>? BarcodeRows { get; set; }

    // ── inventory-import (Đợt 22.5) ──────────────────────────────────────────
    public long? InventoryId { get; set; }
    public List<InventoryImportRow>? InventoryRows { get; set; }
}

/// <summary>1 dòng nhập Excel đánh lại mã ĐKCB hàng loạt — validate giống <c>BarcodeReRegisterService.ExecuteAsync</c>
/// đơn lẻ (mã cũ tồn tại đúng đơn vị, mã mới chưa trùng trong đơn vị).</summary>
public class BarcodeReRegisterRow
{
    public string? OldBarcode { get; set; }
    public string? NewBarcode { get; set; }
    public long? StoreId { get; set; }
}

/// <summary>1 dòng nhập Excel danh sách mã đã quét cho 1 phiên kiểm kê — thay quét tay từng mã qua UI.</summary>
public class InventoryImportRow
{
    public string? Barcode { get; set; }
    public long? StoreId { get; set; }
}

/// <summary>Bảng đối chiếu trước/sau cho 1 lượt nhập độc giả hàng loạt — hiển thị ở màn xem trước, ký HMAC
/// (<c>AdminMutationGuard</c>) để phát hiện dữ liệu đổi giữa lúc xem trước và lúc xác nhận.</summary>
public class ReaderMutationReview
{
    public List<ReaderChangePreview> Changes { get; set; } = new();
}

public class ReaderChangePreview
{
    public string? Cardno { get; set; }
    public bool IsNew { get; set; }
    public List<ReaderFieldChange> Fields { get; set; } = new();
}

public class ReaderFieldChange
{
    public string Field { get; set; } = "";
    public string? Before { get; set; }
    public string? After { get; set; }
}
