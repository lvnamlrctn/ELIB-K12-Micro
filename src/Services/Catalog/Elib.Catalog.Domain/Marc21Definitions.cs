namespace Elib.Catalog.Domain;

public sealed record MarcSubfieldDefinition(string Code, string Name, bool Repeatable);

/// <summary>Định nghĩa một trường MARC21: tên tiếng Việt, lặp được không, ý nghĩa hai chỉ thị, các trường con.</summary>
public sealed record MarcFieldDefinition(
    string Tag, string Name, bool Repeatable, string? Ind1, string? Ind2, IReadOnlyList<MarcSubfieldDefinition> Subfields);

/// <summary>
/// Từ điển MARC21 thư mục dùng chung mọi đơn vị (monolith: Marc_Field/Marc_Sub_Field/Marc_Indicator — đặt theo đơn vị nhưng
/// thực tế là chuẩn chung). Gồm các trường thư viện trường học hay dùng; trường ngoài danh sách vẫn nhập tay được.
/// </summary>
public static class Marc21Definitions
{
    public static IReadOnlyList<MarcFieldDefinition> Fields { get; } =
    [
        C("001", "Số kiểm soát (MFN)"),
        C("003", "Mã cơ quan tạo số kiểm soát"),
        C("005", "Ngày giờ giao dịch gần nhất"),
        C("006", "Đặc trưng tài liệu bổ sung"),
        C("007", "Đặc trưng vật lý"),
        C("008", "Yếu tố dữ liệu độ dài cố định"),
        F("020", "Chỉ số ISBN", true, null, null, "a:ISBN", "c:Giá tiền", "q:Thông tin bổ sung", "z:ISBN huỷ/sai:R"),
        F("022", "Chỉ số ISSN", true, "Mức độ quan tâm quốc tế", null, "a:ISSN", "y:ISSN sai:R", "z:ISSN huỷ:R"),
        F("040", "Nguồn biên mục", false, null, null, "a:Cơ quan biên mục gốc", "b:Ngôn ngữ biên mục", "c:Cơ quan chuyển dạng", "d:Cơ quan sửa đổi:R", "e:Quy tắc mô tả:R"),
        F("041", "Mã ngôn ngữ", true, "Bản dịch", "Nguồn mã", "a:Ngôn ngữ chính văn:R", "b:Ngôn ngữ tóm tắt:R", "h:Ngôn ngữ nguyên bản:R"),
        F("044", "Mã nước xuất bản", false, null, null, "a:Mã nước:R"),
        F("080", "Chỉ số phân loại UDC", true, null, null, "a:Chỉ số UDC", "b:Số định danh tài liệu", "2:Lần xuất bản UDC"),
        F("082", "Chỉ số phân loại DDC", true, "Loại ấn bản", "Nguồn chỉ số", "a:Chỉ số DDC:R", "b:Số định danh tài liệu (số Cutter)", "2:Lần xuất bản DDC"),
        F("084", "Chỉ số phân loại khác (BBK, khung 19 dãy…)", true, null, null, "a:Chỉ số phân loại:R", "b:Số định danh tài liệu", "2:Nguồn khung phân loại"),
        F("090", "Ký hiệu xếp giá (cục bộ)", true, null, null, "a:Ký hiệu phân loại", "b:Ký hiệu tác giả/Cutter"),
        F("100", "Tiêu đề chính — Tên cá nhân", false, "Dạng tên (0: tên riêng, 1: họ trước)", null,
            "a:Tên cá nhân", "b:Số thứ tự", "c:Danh hiệu, chức vụ:R", "d:Năm sinh – năm mất", "e:Thuật ngữ trách nhiệm:R", "q:Dạng đầy đủ của tên"),
        F("110", "Tiêu đề chính — Tên tập thể", false, "Dạng tên", null, "a:Tên tập thể", "b:Đơn vị trực thuộc:R", "e:Thuật ngữ trách nhiệm:R"),
        F("111", "Tiêu đề chính — Tên hội nghị", false, "Dạng tên", null, "a:Tên hội nghị", "c:Địa điểm", "d:Thời gian", "n:Số thứ tự:R"),
        F("130", "Tiêu đề chính — Nhan đề thống nhất", false, "Số ký tự không sắp xếp", null, "a:Nhan đề thống nhất", "l:Ngôn ngữ"),
        F("240", "Nhan đề thống nhất", false, "Hiển thị", "Số ký tự không sắp xếp", "a:Nhan đề thống nhất", "l:Ngôn ngữ"),
        F("242", "Nhan đề dịch", true, "Lập tiêu đề bổ sung", "Số ký tự không sắp xếp", "a:Nhan đề dịch", "b:Phần còn lại của nhan đề", "y:Mã ngôn ngữ nhan đề dịch"),
        F("245", "Nhan đề và thông tin trách nhiệm", false, "Lập tiêu đề bổ sung cho nhan đề", "Số ký tự không sắp xếp",
            "a:Nhan đề chính", "b:Phần còn lại của nhan đề (nhan đề song song, phụ đề)", "c:Thông tin trách nhiệm", "n:Số phần/tập:R", "p:Tên phần/tập:R"),
        F("246", "Dạng khác của nhan đề", true, "Phụ chú/tiêu đề bổ sung", "Loại nhan đề", "a:Nhan đề", "b:Phần còn lại của nhan đề"),
        F("250", "Thông tin lần xuất bản", false, null, null, "a:Lần xuất bản", "b:Thông tin trách nhiệm của lần xuất bản"),
        F("260", "Địa chỉ xuất bản, phát hành", true, null, null, "a:Nơi xuất bản:R", "b:Nhà xuất bản:R", "c:Năm xuất bản:R", "e:Nơi in", "f:Nhà in", "g:Năm in"),
        F("264", "Sản xuất, xuất bản, phát hành (RDA)", true, null, "Chức năng (1: xuất bản)", "a:Nơi xuất bản:R", "b:Nhà xuất bản:R", "c:Năm xuất bản:R"),
        F("300", "Mô tả vật lý", true, null, null, "a:Số trang/khối lượng:R", "b:Đặc điểm vật lý khác (minh hoạ)", "c:Khổ:R", "e:Tài liệu kèm theo"),
        F("310", "Định kỳ hiện tại", false, null, null, "a:Định kỳ", "b:Ngày áp dụng"),
        F("362", "Thời gian xuất bản / số thứ tự", true, "Dạng ngày", null, "a:Thời gian xuất bản và/hoặc số thứ tự"),
        F("490", "Thông tin tùng thư", true, "Tùng thư có lập tiêu đề", null, "a:Tên tùng thư:R", "v:Số thứ tự trong tùng thư:R", "x:ISSN tùng thư"),
        F("500", "Phụ chú chung", true, null, null, "a:Phụ chú chung"),
        F("502", "Phụ chú luận văn, luận án", true, null, null, "a:Phụ chú luận văn", "b:Loại học vị", "c:Cơ sở đào tạo", "d:Năm bảo vệ"),
        F("504", "Phụ chú thư mục", true, null, null, "a:Phụ chú thư mục"),
        F("505", "Phụ chú nội dung", true, "Kiểm soát hiển thị", null, "a:Phụ chú nội dung"),
        F("520", "Tóm tắt", true, "Kiểm soát hiển thị", null, "a:Tóm tắt", "b:Mở rộng tóm tắt"),
        F("521", "Đối tượng sử dụng", true, "Kiểm soát hiển thị", null, "a:Đối tượng sử dụng (lứa tuổi, cấp học):R"),
        F("526", "Phụ chú chương trình học", true, "Kiểm soát hiển thị", null, "a:Tên chương trình", "b:Mức độ/lớp", "c:Môn học"),
        F("546", "Phụ chú ngôn ngữ", true, null, null, "a:Phụ chú ngôn ngữ"),
        F("600", "Tiêu đề bổ sung chủ đề — Tên cá nhân", true, "Dạng tên", "Hệ thống tiêu đề chủ đề", "a:Tên cá nhân", "d:Năm sinh – năm mất", "x:Đề mục con chung:R"),
        F("610", "Tiêu đề bổ sung chủ đề — Tên tập thể", true, "Dạng tên", "Hệ thống tiêu đề chủ đề", "a:Tên tập thể", "b:Đơn vị trực thuộc:R", "x:Đề mục con chung:R"),
        F("650", "Tiêu đề bổ sung chủ đề — Thuật ngữ chủ đề", true, "Cấp độ chủ đề", "Hệ thống tiêu đề chủ đề",
            "a:Thuật ngữ chủ đề", "v:Đề mục con hình thức:R", "x:Đề mục con chung:R", "y:Đề mục con thời gian:R", "z:Đề mục con địa lý:R", "2:Nguồn đề mục"),
        F("651", "Tiêu đề bổ sung chủ đề — Địa danh", true, null, "Hệ thống tiêu đề chủ đề", "a:Địa danh", "x:Đề mục con chung:R", "y:Đề mục con thời gian:R"),
        F("653", "Thuật ngữ chỉ mục — Từ khoá tự do", true, "Cấp độ", "Loại thuật ngữ", "a:Từ khoá:R"),
        F("655", "Thuật ngữ chỉ mục — Thể loại/hình thức", true, null, "Nguồn thuật ngữ", "a:Thể loại/hình thức", "2:Nguồn thuật ngữ"),
        F("700", "Tiêu đề bổ sung — Tên cá nhân", true, "Dạng tên", "Loại tiêu đề bổ sung",
            "a:Tên cá nhân", "d:Năm sinh – năm mất", "e:Thuật ngữ trách nhiệm (chủ biên, dịch…):R"),
        F("710", "Tiêu đề bổ sung — Tên tập thể", true, "Dạng tên", "Loại tiêu đề bổ sung", "a:Tên tập thể", "b:Đơn vị trực thuộc:R", "e:Thuật ngữ trách nhiệm:R"),
        F("740", "Tiêu đề bổ sung — Nhan đề liên quan/phân tích", true, "Số ký tự không sắp xếp", "Loại tiêu đề bổ sung", "a:Nhan đề", "n:Số phần/tập:R", "p:Tên phần/tập:R"),
        F("773", "Liên kết tài liệu chủ (bài trích)", true, "Kiểm soát phụ chú", null,
            "t:Nhan đề tài liệu chủ", "d:Nơi, nhà xuất bản, năm", "g:Thông tin liên quan (số, trang):R", "x:ISSN", "z:ISBN:R"),
        F("830", "Tiêu đề bổ sung tùng thư — Nhan đề thống nhất", true, null, "Số ký tự không sắp xếp", "a:Nhan đề thống nhất", "v:Số thứ tự:R"),
        F("852", "Nơi lưu giữ", true, "Sơ đồ xếp giá", "Thứ tự xếp giá",
            "a:Cơ quan lưu giữ", "b:Kho/bộ sưu tập:R", "c:Vị trí giá:R", "h:Ký hiệu phân loại", "i:Ký hiệu tài liệu:R", "j:Số đăng ký cá biệt"),
        F("856", "Địa chỉ điện tử và truy cập", true, "Phương thức truy cập", "Quan hệ", "u:Đường dẫn (URL):R", "y:Chữ hiển thị liên kết:R", "z:Ghi chú công khai:R"),
        F("911", "Người biên mục (cục bộ)", false, null, null, "a:Người biên mục", "b:Ngày biên mục"),
        F("925", "Dạng tài liệu (cục bộ)", false, null, null, "a:Dạng tài liệu"),
        F("926", "Mức độ mật (cục bộ)", false, null, null, "a:Mức độ mật"),
        F("927", "Mã dạng tài liệu (cục bộ)", false, null, null, "a:Mã dạng tài liệu"),
    ];

    public static MarcFieldDefinition? Find(string tag) => Fields.FirstOrDefault(f => f.Tag == tag);

    /// <summary>Trường điều khiển: chỉ có giá trị, không chỉ thị/trường con.</summary>
    private static MarcFieldDefinition C(string tag, string name) => new(tag, name, Repeatable: tag is "006" or "007", null, null, []);

    /// <summary>Trường con dạng "a:Tên" (không lặp) hoặc "a:Tên:R" (lặp được).</summary>
    private static MarcFieldDefinition F(string tag, string name, bool repeatable, string? ind1, string? ind2, params string[] subfields) =>
        new(tag, name, repeatable, ind1, ind2, [.. subfields.Select(s =>
        {
            var parts = s.Split(':');
            return new MarcSubfieldDefinition(parts[0], parts[1], parts.Length > 2 && parts[2] == "R");
        })]);
}
