using ELIBAPI.Core.Common;
using ELIBAPI.Core.Entities.Cms;

namespace ELIBAPI.Core.Interfaces;

/// <summary>
/// File đính kèm tin tức (cms.AttachFile) — port ELIB-LRC 10-04: upload vào kho lưu trữ riêng tư, tải về qua API (không lộ
/// URL kho), xoá kèm object, đồng bộ file cũ (Url "Upload/...") lên kho khi admin yêu cầu. Không đổi schema: Name = tên
/// hiển thị, Url = tên object trong kho, FileSize = KB, CreatedDate = ngày tải lên.
/// <para>Tenant: <c>tenantId</c> null = tài khoản đặc quyền (mọi đơn vị). File thuộc đơn vị của tin. API công khai nhận
/// đơn vị theo tên miền (<c>hostTenantPublicId</c>, ghi đè bởi PublicHostTenantFilter) — chỉ tin của đơn vị đó hoặc dùng chung.</para>
/// </summary>
public interface IAttachFileService
{
    Task<ServiceResult<AttachFile>> UploadAsync(Stream content, string fileName, string contentType, long length, Guid newsPublicId,
        string? displayName, long? tenantId, long? userId);

    /// <summary>Mở file cho admin (phạm vi đơn vị qua repository).</summary>
    Task<ServiceResult<AttachFileContent>> OpenAsync(Guid filePublicId);

    /// <summary>Mở file cho bạn đọc: chỉ file của tin đã xuất bản, đúng đơn vị theo tên miền.</summary>
    Task<ServiceResult<AttachFileContent>> OpenPublicAsync(Guid filePublicId, Guid? hostTenantPublicId);

    /// <summary>File của 1 tin đã xuất bản (OPAC) — rỗng nếu tin không tồn tại/chưa xuất bản/khác đơn vị.</summary>
    Task<List<AttachFile>> PublishedFilesAsync(Guid newsPublicId, Guid? hostTenantPublicId);

    Task<ServiceResult<bool>> RenameAsync(Guid filePublicId, string name);

    Task<ServiceResult<bool>> DeleteAsync(Guid filePublicId);

    /// <summary>Chép file cũ (dưới <c>PathSettings:PathAttachment</c>) của đơn vị lên kho riêng tư và cập nhật Url.</summary>
    Task<ServiceResult<AttachFileSyncResult>> SyncLegacyFilesAsync(long? tenantId);
}

/// <summary>Nội dung file: stream (người gọi dispose), kiểu nội dung và tên tải về.</summary>
public sealed record AttachFileContent(Stream Content, string ContentType, string DownloadName);

public sealed record AttachFileSyncResult(int Synced, int Failed, int Total, List<string> Errors);
