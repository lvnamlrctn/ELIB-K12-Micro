#r "nuget: ClosedXML, 0.104.2"
using ClosedXML.Excel;

var basePath = Path.GetDirectoryName(Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ?? ".")
            ?? Directory.GetCurrentDirectory();

// ── 1. Import Reader ─────────────────────────────────────────────────────────
using (var wb = new XLWorkbook())
{
    var ws = wb.Worksheets.Add("Bạn đọc");
    var headers = new[] { "STT", "Số thẻ", "Họ", "Tên", "Giới tính", "Ngày sinh",
        "Email", "Mật khẩu", "Điện thoại", "Địa chỉ", "Ngày cấp", "Ngày hết hạn",
        "Lớp", "Khóa", "Đơn vị" };
    for (int i = 0; i < headers.Length; i++)
        ws.Cell(1, i + 1).Value = headers[i];

    var row1 = new object[] { 1, "TV001", "Nguyễn Văn", "An", "Nam", "15/03/2000",
        "an.nv@email.com", "123456", "0901234567", "123 Lê Lợi, TP.HCM",
        "01/09/2025", "01/09/2026", "CNTT-K20A", "K20", "Khoa CNTT" };
    var row2 = new object[] { 2, "TV002", "Trần Thị", "Bình", "Nữ", "20/07/2001",
        "binh.tt@email.com", "abc123", "0912345678", "45 Hai Bà Trưng, HN",
        "01/09/2025", "01/09/2026", "KT-K21B", "K21", "Khoa Kinh tế" };
    var row3 = new object[] { 3, "TV003", "Lê Hoàng", "Cường", "", "10/01/1999",
        "", "pass999", "", "", "15/10/2025", "15/10/2026", "", "", "Thư viện" };

    var rows = new[] { row1, row2, row3 };
    for (int r = 0; r < rows.Length; r++)
        for (int c = 0; c < rows[r].Length; c++)
            ws.Cell(r + 2, c + 1).Value = rows[r][c]?.ToString() ?? "";

    ws.Row(1).Style.Font.Bold = true;
    ws.Row(1).Style.Fill.BackgroundColor = XLColor.LightBlue;
    ws.Columns().AdjustToContents();
    wb.SaveAs(Path.Combine(basePath, "import-reader-mau.xlsx"));
}

// ── 2. Import Users ──────────────────────────────────────────────────────────
using (var wb = new XLWorkbook())
{
    var ws = wb.Worksheets.Add("Tài khoản");
    var headers = new[] { "FullName", "LoginName", "Password", "Email", "Phone",
        "Address", "PortalId", "Language", "RoleId", "Status" };
    for (int i = 0; i < headers.Length; i++)
        ws.Cell(1, i + 1).Value = headers[i];

    var row1 = new object[] { "Nguyễn Văn An", "admin_an", "123456", "an@email.com",
        "0901234567", "123 Lê Lợi, TP.HCM", "", "vi", "1", "2" };
    var row2 = new object[] { "Trần Thị Bình", "user_binh", "abc123", "binh@email.com",
        "0912345678", "45 Hai Bà Trưng, HN", "", "vi", "2", "2" };
    var row3 = new object[] { "Lê Hoàng Cường", "user_cuong", "pass999", "",
        "", "", "", "vi", "2", "1" };

    var rows = new[] { row1, row2, row3 };
    for (int r = 0; r < rows.Length; r++)
        for (int c = 0; c < rows[r].Length; c++)
            ws.Cell(r + 2, c + 1).Value = rows[r][c]?.ToString() ?? "";

    ws.Row(1).Style.Font.Bold = true;
    ws.Row(1).Style.Fill.BackgroundColor = XLColor.LightGreen;
    ws.Columns().AdjustToContents();
    wb.SaveAs(Path.Combine(basePath, "import-users-mau.xlsx"));
}

Console.WriteLine("Done: import-reader-mau.xlsx, import-users-mau.xlsx");
