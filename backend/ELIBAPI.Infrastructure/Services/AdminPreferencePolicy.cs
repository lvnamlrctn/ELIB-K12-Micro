using System.Text.Json;

namespace ELIBAPI.Infrastructure.Services;

/// <summary>Danh sách cho phép của cấu hình bảng theo tài khoản (Đợt 21 — port từ ELIB-LRC, đổi tên cột/bộ lọc
/// theo đúng khoá <c>matColumnDef</c>/<c>searchForm</c> của ELIB). Cột chọn/STT/đơn vị/thao tác luôn cố định nên
/// không nằm trong danh sách. Không chấp nhận trường lạ — payload chỉ là dữ liệu hiển thị, không bao giờ là userId.</summary>
public static class AdminPreferencePolicy
{
    public static string Module(string page) => page switch
    {
        "readers" => "READERS", "catalog-bibs" => "CATALOG_BIBS", "ab-receipts" => "AB_RECEIPTS",
        _ => throw new ArgumentException("Trang không hỗ trợ lưu cấu hình.")
    };

    public static bool Supports(string page) => Columns.ContainsKey(page);

    private static readonly Dictionary<string, string[]> Columns = new()
    {
        ["readers"] = "cardno,fullName,readerType,issueDate,expireDate,status,gender,birthDate,citizenId,email,phone,address,class,course,department".Split(','),
        ["catalog-bibs"] = "mfn,title,author,publisher,publishDate,status,hasReceipt,hasOrder,bibType".Split(','),
        ["ab-receipts"] = "code,receiptName,supplier,store,receiptDate,status".Split(','),
    };

    private static readonly Dictionary<string, string[]> Filters = new()
    {
        ["readers"] = "firstName,lastName,cardno,classId,courseId,readerTypeId,status,issuedFrom,issuedTo,expiredFrom,expiredTo,tenantId".Split(','),
        ["catalog-bibs"] = "keyword,title,author,publisher,mfnFrom,mfnTo,bibTypeId".Split(','),
        ["ab-receipts"] = "keyword,codeFrom,codeTo,receiptName,createdBy,supplierId,status,receiptDateFrom,receiptDateTo,createdDateFrom,createdDateTo,sourceId,fundId".Split(','),
    };

    private static readonly int[] PageSizes = [5, 10, 20, 25, 50, 100];

    public static void Validate(string page, JsonElement settings)
    {
        Module(page);
        if (settings.ValueKind != JsonValueKind.Object || settings.GetRawText().Length > 16000)
            throw new ArgumentException("Cấu hình không hợp lệ.");
        foreach (var p in settings.EnumerateObject())
            if (p.Name is not ("version" or "pageSize" or "columns" or "filters"))
                throw new ArgumentException("Trường cấu hình không được hỗ trợ.");
        if (!settings.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version != 1)
            throw new ArgumentException("Phiên bản cấu hình không hợp lệ.");
        if (!settings.TryGetProperty("pageSize", out var size) || size.ValueKind != JsonValueKind.Number || !size.TryGetInt32(out var n) || !PageSizes.Contains(n))
            throw new ArgumentException("Số dòng không hợp lệ.");
        if (!settings.TryGetProperty("columns", out var cols) || cols.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("Danh sách cột không hợp lệ.");
        var seen = new HashSet<string>();
        foreach (var c in cols.EnumerateArray())
            if (c.ValueKind != JsonValueKind.String || !Columns[page].Contains(c.GetString()) || !seen.Add(c.GetString()!))
                throw new ArgumentException("Cột không hợp lệ hoặc trùng.");
        if (!settings.TryGetProperty("filters", out var filters) || filters.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Bộ lọc không hợp lệ.");
        foreach (var f in filters.EnumerateObject())
        {
            if (!Filters[page].Contains(f.Name) || f.Value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Number or JsonValueKind.String))
                throw new ArgumentException("Bộ lọc không được hỗ trợ.");
            if (f.Value.ValueKind == JsonValueKind.String && f.Value.GetString()!.Length > 250)
                throw new ArgumentException("Giá trị bộ lọc quá dài.");
        }
    }
}
