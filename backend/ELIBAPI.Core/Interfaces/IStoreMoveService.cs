using ELIBAPI.Core.Common;
using ELIBAPI.Core.DTOs.Request;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// Phiếu điều chuyển kho (PrintBook.ab_move / ab_move_detail): chọn ĐKCB thuộc kho nguồn của phiếu (<c>StoreDeliver_Id</c>)
/// để chuyển sang kho nhận (<c>StoreReceipt_Id</c>). Mã phiếu (<c>Code</c>) luôn bằng Id. Port ELIB-LRC 10-04.
/// Đa đơn vị: <c>tenantId</c> = đơn vị JWT (null = tài khoản hệ thống); phiếu khác đơn vị coi như không tồn tại; 2 kho của
/// phiếu phải thuộc đơn vị phiếu (hoặc dùng chung).
/// </summary>
public interface IStoreMoveService
{
    /// <summary>Ràng buộc khi thêm/sửa phiếu: kho nguồn khác kho nhận; phiếu đã có dòng thì không đổi kho nguồn.</summary>
    Task<string?> ValidateAsync(AbMoveRequest request, Guid? publicId, long? tenantId);

    /// <summary>Gán <c>Code = Id</c> (Id chỉ có sau khi insert).</summary>
    Task SyncCodeAsync(long moveId);

    /// <summary>ĐKCB thuộc kho nguồn của phiếu, chưa có trong phiếu. Lọc nhan đề/tác giả/NXB không phân biệt hoa thường.</summary>
    Task<ServiceResult<(List<MoveCandidate> Items, int Total)>> SearchDocumentsAsync(MoveDocumentQuery query, long? tenantId);

    Task<ServiceResult<List<MoveLine>>> LinesAsync(long moveId, long? tenantId);

    /// <summary>Chỉ thêm ĐKCB còn hiệu lực và đang ở kho nguồn của phiếu; trả số đã thêm và số bị bỏ qua.</summary>
    Task<ServiceResult<MoveAddResult>> AddDetailsAsync(long moveId, IReadOnlyCollection<long> barcodeIds, long? userId, long? tenantId);

    /// <summary>Hoàn thành điều chuyển: ĐKCB trong phiếu chuyển sang kho nhận (bỏ vị trí giá cũ), phiếu ghi ngày/người nhận và
    /// khoá. Bỏ qua (và báo lại) bản đã xoá, không còn ở kho nguồn, đang mượn, đang ra kho, đã mất/thanh lý.</summary>
    Task<ServiceResult<MoveCompleteResult>> CompleteAsync(long moveId, long? userId, long? tenantId);

    /// <summary>Lỗi nếu phiếu (theo PublicId phiếu hoặc PublicId dòng) đã hoàn thành — phiếu đã hoàn thành không sửa/xoá được.</summary>
    Task<string?> LockedErrorAsync(Guid? movePublicId = null, Guid? linePublicId = null, long? moveId = null);
}

public sealed record MoveDocumentQuery(long MoveId, long? MfnFrom, long? MfnTo, string? Title, string? Author, string? Publisher,
    string? PublishYear, string? BarcodeFrom, string? BarcodeTo, int? PageIndex, int? PageSize);

public sealed record MoveCandidate(long Id, string? Barcode, long? Mfn, string? Title, string? Author, string? Publisher, string? PublishYear);

public sealed record MoveLine(long Id, Guid PublicId, string? Barcode, string? BibTitle, string? BibAuthor);

public sealed record MoveAddResult(int Added, int Skipped);

public sealed record MoveSkippedCopy(string? Barcode, string Reason);

public sealed record MoveCompleteResult(int Moved, List<MoveSkippedCopy> Skipped);
