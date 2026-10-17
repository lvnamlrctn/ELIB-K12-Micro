using Elib.BuildingBlocks.Authorization;
using Elib.BuildingBlocks.Crud;
using Elib.Digital.Application;

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
        var api = app.MapGroup("/api").RequireAuthorization(new RequiresModuleAttribute(Module));

        api.MapCrud<EbookCollectionResource>("/collections", "DIGITAL_COLLECTIONS").WithTags("Collections");
        api.MapCrud<EbookSubjectResource>("/subjects", "DIGITAL_SUBJECTS").WithTags("Subjects");
        api.MapCrud<EbookTopicResource>("/topics", "DIGITAL_TOPICS").WithTags("Topics");
        api.MapCrud<EbookDigTypeResource>("/dig-types", "DIGITAL_DIG_TYPES").WithTags("DigTypes");
        api.MapCrud<PolicyDigitalResource>("/policies", "DIGITAL_POLICIES").WithTags("Policies");
        api.MapCrud<PolicyDigitalByCollectionResource>("/policies-by-collection", "DIGITAL_POLICIES_BY_COLLECTION").WithTags("PoliciesByCollection");

        return app;
    }
}
