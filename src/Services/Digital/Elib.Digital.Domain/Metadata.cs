using System.Globalization;
using Elib.BuildingBlocks.Domain;

namespace Elib.Digital.Domain;

/// <summary>Kiểu nhập của một trường metadata (LRC: MetaDataFieldRegistery.Input là chuỗi tự do — K12 chuẩn hoá thành tập hữu hạn để kiểm giá trị).</summary>
public static class MetadataInputs
{
    public const string Text = "text";
    public const string TextArea = "textarea";
    public const string Date = "date";
    public const string Number = "number";

    public static readonly IReadOnlyList<string> All = [Text, TextArea, Date, Number];

    public static string Normalize(string? input) =>
        string.IsNullOrWhiteSpace(input) ? Text : All.FirstOrDefault(x => string.Equals(x, input.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new BusinessRuleException("METADATA_INPUT_INVALID", $"Kiểu nhập '{input}' không hợp lệ (text, textarea, date, number).");

    /// <summary>Kiểm giá trị theo kiểu nhập: date nhận yyyy | yyyy-MM | yyyy-MM-dd; number nhận số (dấu chấm thập phân).</summary>
    public static void Validate(string input, string value, string fieldLabel)
    {
        var ok = input switch
        {
            Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            Date => DateTimeOffset.TryParseExact(value, ["yyyy", "yyyy-MM", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => true,
        };
        if (!ok) throw new BusinessRuleException("METADATA_VALUE_INVALID", $"Giá trị của '{fieldLabel}' không đúng kiểu {input}.");
    }
}

/// <summary>Lược đồ metadata (LRC: MetadataSchemaRegistry — NameSpace/ShortId/Language, ví dụ Dublin Core "dc"). Gom các <see cref="MetadataField"/>.</summary>
public sealed class MetadataSchema : TenantEntity, IHasStatus
{
    public const int MaxLength = 200;

    private MetadataSchema() { }

    public string NameSpace { get; private set; } = "";
    public string ShortId { get; private set; } = "";
    public string? Language { get; private set; }
    public int Status { get; private set; } = IHasStatus.Active;

    public static MetadataSchema Create(string? nameSpace, string? shortId, string? language, int status)
    {
        var schema = new MetadataSchema();
        schema.Update(nameSpace, shortId, language, status);
        return schema;
    }

    public void Update(string? nameSpace, string? shortId, string? language, int status)
    {
        NameSpace = TextRules.Required(nameSpace, MaxLength, "SCHEMA_NAMESPACE_INVALID", "Namespace");
        ShortId = TextRules.Required(shortId, 50, "SCHEMA_SHORT_ID_INVALID", "Mã ngắn");
        Language = TextRules.Optional(language, 10, "LANGUAGE_INVALID", "Ngôn ngữ");
        Status = StatusRules.Validate(status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);
}

/// <summary>Trường metadata (LRC: MetaDataFieldRegistery — Field/Subfield, mô tả vi/en, Input, ExportField). Một trường lặp được nhiều giá trị.</summary>
public sealed class MetadataField : TenantEntity, IHasStatus
{
    private MetadataField() { }

    public long SchemaId { get; private set; }
    public string Field { get; private set; } = "";
    public string? Subfield { get; private set; }
    public string DescriptionVn { get; private set; } = "";
    public string? DescriptionEn { get; private set; }
    public int SortOrder { get; private set; }
    public string Input { get; private set; } = MetadataInputs.Text;

    /// <summary>Xuất ra metadata chuẩn (LRC: ExportField 1 = xuất; K12 giữ nguyên số vì LRC chưa chuẩn hoá thành cờ).</summary>
    public int? ExportField { get; private set; }

    public int Status { get; private set; } = IHasStatus.Active;

    public static MetadataField Create(long schemaId, MetadataFieldFields f)
    {
        var field = new MetadataField { SchemaId = schemaId };
        field.Update(f);
        return field;
    }

    public void Update(MetadataFieldFields f)
    {
        ArgumentNullException.ThrowIfNull(f);
        Field = TextRules.Required(f.Field, 100, "FIELD_INVALID", "Trường");
        Subfield = TextRules.Optional(f.Subfield, 100, "SUBFIELD_INVALID", "Trường con");
        DescriptionVn = TextRules.Required(f.DescriptionVn, 300, "DESCRIPTION_INVALID", "Mô tả (vi)");
        DescriptionEn = TextRules.Optional(f.DescriptionEn, 300, "DESCRIPTION_INVALID", "Mô tả (en)");
        SortOrder = f.SortOrder;
        Input = MetadataInputs.Normalize(f.Input);
        ExportField = f.ExportField;
        Status = StatusRules.Validate(f.Status);
    }

    public void ChangeStatus(int status) => Status = StatusRules.Validate(status);

    /// <summary>Nhãn hiển thị trong thông báo lỗi và chỉ mục tìm kiếm.</summary>
    public string Label => Subfield is { Length: > 0 } sub ? $"{Field}.{sub}" : Field;
}

public sealed record MetadataFieldFields(
    string Field, string? Subfield, string DescriptionVn, string? DescriptionEn, int SortOrder = 0, string? Input = null, int? ExportField = null,
    int Status = IHasStatus.Active);

/// <summary>Giá trị metadata của một tài liệu số (LRC: MetaDataValue). Một trường có thể có nhiều dòng (SortOrder); Language riêng từng giá trị.</summary>
public sealed class EbookMetadataValue : TenantEntity
{
    public const int MaxValueLength = 2000;

    private EbookMetadataValue() { }

    public long EbookItemId { get; private set; }
    public long FieldId { get; private set; }
    public string Value { get; private set; } = "";
    public string? Language { get; private set; }
    public int SortOrder { get; private set; }

    public static EbookMetadataValue Create(long ebookItemId, MetadataField field, string? value, string? language, int sortOrder)
    {
        ArgumentNullException.ThrowIfNull(field);
        var text = TextRules.Required(value, MaxValueLength, "METADATA_VALUE_INVALID", field.Label);
        MetadataInputs.Validate(field.Input, text, field.Label);
        return new EbookMetadataValue
        {
            EbookItemId = ebookItemId,
            FieldId = field.Id,
            Value = text,
            Language = TextRules.Optional(language, 10, "LANGUAGE_INVALID", "Ngôn ngữ"),
            SortOrder = sortOrder,
        };
    }
}
