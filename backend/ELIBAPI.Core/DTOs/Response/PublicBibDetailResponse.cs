namespace ELIBAPI.Core.DTOs.Response;

/// <summary>Chi tiết 1 biểu ghi tài liệu in (PrintBook.Bib) cho trang chi tiết OPAC công khai.</summary>
public class PublicBibDetailResponse
{
    public Guid  PublicId { get; set; }
    public long  BibId    { get; set; }
    public long? Mfn      { get; set; }

    public string? Title       { get; set; }
    public string? Author      { get; set; }
    public string? Publisher   { get; set; }
    public string? PublishDate { get; set; }
    public int?    PublishYear { get; set; }
    public string? Ddc         { get; set; }
    /// <summary>Chỉ số Cutter (MARC 082$b) — hiển thị cạnh DDC làm chỉ số xếp giá.</summary>
    public string? Cutter      { get; set; }
    public string? Isbn        { get; set; }
    public string? Summary     { get; set; }
    public string? Keyword     { get; set; }
    public string? Language    { get; set; }
    public string? MaterialType { get; set; }
    public string? CollectionId   { get; set; }
    public string? CollectionName { get; set; }
    public string? Contributor { get; set; }
    public string? Images      { get; set; }
    public string? Isbd        { get; set; }
    public string? Status      { get; set; }
    public string? StatusName  { get; set; }
    public string? Url         { get; set; }

    public int CopyCount      { get; set; }
    public int AvailableCount { get; set; }

    public List<PublicBibHoldingResponse>     Holdings     { get; set; } = [];
    public List<PublicBibControlFieldResponse> ControlFields { get; set; } = [];
    public List<PublicBibMarcFieldResponse>   MarcFields   { get; set; } = [];
}

public class PublicBibHoldingResponse
{
    public long    BarcodeId  { get; set; }
    public string? Barcode    { get; set; }
    public string? StoreName  { get; set; }
    public string? Status     { get; set; }
    public string? StatusName { get; set; }
}

/// <summary>Trường điều khiển MARC (000 Leader, 001, 003, 005, 008...) — không có chỉ số/tiểu trường.</summary>
public class PublicBibControlFieldResponse
{
    public string  Field       { get; set; } = "";
    public string  Value       { get; set; } = "";
    public string? Description { get; set; }
}

public class PublicBibMarcFieldResponse
{
    public string  Field       { get; set; } = "";
    public string  Indicator1  { get; set; } = "";
    public string  Indicator2  { get; set; } = "";
    public string  SubField    { get; set; } = "";
    public string  Data        { get; set; } = "";
    public string? FieldDescription    { get; set; }
    public string? SubFieldDescription { get; set; }
}
