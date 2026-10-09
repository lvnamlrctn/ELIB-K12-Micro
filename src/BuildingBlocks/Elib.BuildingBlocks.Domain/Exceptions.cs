namespace Elib.BuildingBlocks.Domain;

/// <summary>
/// Vi phạm quy tắc nghiệp vụ — trả về client dạng problem+json với <see cref="Code"/> ổn định để frontend dịch,
/// ví dụ <c>LOAN_LIMIT_EXCEEDED</c>, <c>ITEM_NOT_AVAILABLE</c> (docs 07 §4).
/// </summary>
public class BusinessRuleException(string code, string message, int statusCode = 400) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>400 (mặc định), 409 (xung đột trạng thái) hoặc 422.</summary>
    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string entity, object key)
    : BusinessRuleException("NOT_FOUND", $"Không tìm thấy {entity} '{key}'.", 404);

public sealed class ConflictException(string code, string message) : BusinessRuleException(code, message, 409);
