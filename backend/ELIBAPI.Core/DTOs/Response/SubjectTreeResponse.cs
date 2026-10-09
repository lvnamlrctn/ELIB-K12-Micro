namespace ELIBAPI.Core.DTOs.Response;

/// <summary>1 node trong cây "Cơ sở môn học" (Đợt 8): Org (Đơn vị đào tạo) → Chương trình → Ngành →
/// Môn học → Tài liệu. Chỉ dùng cho GetFullTree (đọc-only) — mọi thao tác ghi từng node đi qua CRUD
/// sẵn có của entity tương ứng (OrgController, EvaluateProgramController, NganhHocController,
/// MonHocController, NganhMonHocController, TaiLieuController).</summary>
public class SubjectTreeNode
{
    public string   NodeType     { get; set; } = "";
    public long     Id           { get; set; }
    public Guid     PublicId     { get; set; }
    public string?  Name         { get; set; }
    public string?  Code         { get; set; }
    public int?     SoTinChi     { get; set; }
    public Guid?    LinkPublicId { get; set; }
    public string?  Author       { get; set; }
    public string?  PublishDate  { get; set; }
    public int?     LoaiTaiLieu  { get; set; }
    public long?    TenantId     { get; set; }
    public string?  TenantName   { get; set; }
    public List<SubjectTreeNode> Children { get; set; } = [];
}
