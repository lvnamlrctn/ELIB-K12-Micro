using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ELIBAPI.Core.Entities.Dbo;

[Table("Reader", Schema = "dbo")]
public class Reader
{
    [Key] public long      Id                     { get; set; }
    public string?   FirstName              { get; set; }
    public string?   LastName               { get; set; }
    public string?   Cardno                 { get; set; }
    /// <summary>Số căn cước công dân (12 số).</summary>
    [MaxLength(20)] public string? CitizenId { get; set; }
    /// <summary>UID chip thẻ (RFID/NFC) do đầu đọc của hệ thống kiểm soát cửa gửi lên — dạng chuẩn hoá <see cref="Core.Common.CardUid"/>.
    /// Không trùng giữa các bạn đọc cùng đơn vị.</summary>
    [MaxLength(64)] public string? CardUid { get; set; }
    public string?   Email                  { get; set; }
    public string?   Phone                  { get; set; }
    public string?   Address                { get; set; }
    public long?     OrgId                  { get; set; }
    public long?     ReaderTypeId           { get; set; }
    public long?     ClassId                { get; set; }
    public long?     CourseId               { get; set; }
    public long?     DegreeId               { get; set; }
    public long?     EthenicId              { get; set; }
    public long?     ProfId                 { get; set; }
    public double?   Blane                  { get; set; }
    public DateTime? CreatedDate            { get; set; }
    public DateTime? ExpireDate             { get; set; }
    public DateTime? IssueDate              { get; set; }
    public DateTime? BirthDate              { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string?   Password               { get; set; }
    public string?   PortalId               { get; set; }
    public string?   Language               { get; set; }
    public string?   Photo                  { get; set; }
    public DateTime? LasttimeLogin          { get; set; }
    public DateTime? LastLogin              { get; set; }
    public DateTime? LastUpdate             { get; set; }
    public int?      Status                 { get; set; }
    public string?   LockReason             { get; set; }
    public int?      Sex                    { get; set; }
    public int?      RequiredChangePassword { get; set; }
    public int?      IsChangePassword       { get; set; }
    public long?     CreatedBy              { get; set; }
    public long?     UpdatedBy              { get; set; }
    // Audit Trail
    public int?     IsDelete        { get; set; }
    public long?    CreatedRowBy    { get; set; }
    public long?    UpdateRowBy     { get; set; }
    public DateTime? CreatedRowDate { get; set; }
    public DateTime? UpdatedRowDate { get; set; }
    public long?    TenantId    { get; set; }
    public Guid     PublicId        { get; set; }
    [NotMapped] public string? TenantName { get; set; }
}

