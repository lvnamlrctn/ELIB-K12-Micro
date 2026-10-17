using Elib.BuildingBlocks.Authorization;

namespace Elib.Digital.Api;

/// <summary>
/// Tài liệu số (gateway: /api/admin/digital/** → /api/**, cần license DIGITAL). MVP tham khảo ELIB-LRC: bộ sưu tập/môn loại/chủ đề/loại
/// tài liệu, biểu ghi số + file, chính sách mượn, mượn/trả/đặt trước. Danh mục và nghiệp vụ được thêm dần theo từng lát (xem plan).
/// </summary>
public static class DigitalEndpoints
{
    public const string Module = "DIGITAL";

    public static IEndpointRouteBuilder MapDigitalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));
        return app;
    }
}
