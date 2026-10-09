using ELIBAPI.Core.Entities.Ebook;

namespace ELIBAPI.Core.DTOs.Response;

public class EbookItemDetailResponse
{
    public EbookItem           Item     { get; set; } = null!;
    public List<MetaDataValue> MetaData { get; set; } = [];
}
