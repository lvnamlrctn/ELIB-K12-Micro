using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Ebook;

[Table("PolicyDigitalByCollection", Schema = "Ebook")]
public class PolicyDigitalByCollection
{
    [Key] public int    Id           { get; set; }
    public int?   ReaderTypeid { get; set; }
    public int?    CollectionId { get; set; }
    public int?   Maxpage      { get; set; }
    public double? Maxsize      { get; set; }
    public int?   Maxdocument  { get; set; }
    public int?   Read         { get; set; }
    public int?   Comment      { get; set; }
    public int?   Download     { get; set; }
    public int?   OfflineDays  { get; set; }   // Số ngày Offline mặc định theo (Loại độc giả, Bộ sưu tập) — dự phòng khi EbookItem.OfflineDays không đặt riêng
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
}

