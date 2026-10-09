using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.PrintBook;

[Table("Bib_Type", Schema = "PrintBook")]
public class BibType
{
    // Bảng Bib_Type dùng tên cột viết thường cho các cột nghiệp vụ (id, name, …) — khác các bảng khác;
    // thiếu [Column] thì EF sinh "Id" → lỗi 42703 "column b.Id does not exist".
    [Key, Column("id")] public long Id { get; set; }
    [Column("name")]             public string? Name             { get; set; }
    [Column("code")]             public string? Code             { get; set; }
    [Column("bib_level")]        public string? Bib_Level        { get; set; }
    [Column("record_type_code")] public string? Record_Type_Code { get; set; }
    [Column("materal_type")]     public string? Materal_Type     { get; set; }
    [Column("type")]             public string? Type             { get; set; }
    public int?      IsDelete       { get; set; }
    public long?     CreatedRowBy   { get; set; }
    public long?     UpdateRowBy    { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?     TenantId   { get; set; }
    public Guid      PublicId       { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

