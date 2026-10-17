using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events;
using Elib.Contracts.Events.Patron;
using Elib.Contracts.Events.Platform;
using Elib.Patron.Application;

namespace Elib.Patron.Tests.Api;

public sealed class PatronApiTests : IClassFixture<PatronApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 6000;
    private static long _nextUserId = 16000;

    private readonly PatronApiFactory _factory;

    public PatronApiTests(PatronApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private static Task<HttpResponseMessage> AddReader(HttpClient client, ReaderRequest request) =>
        client.PostAsJsonAsync(U("/api/readers/Add"), request, Json);

    private static async Task<NamedItemDto> AddNamed(HttpClient client, string resource, string name) =>
        await Read<NamedItemDto>(await client.PostAsJsonAsync(U($"/api/{resource}/Add"), new NameRequest(name), Json));

    [Fact]
    public async Task New_tenant_gets_default_reader_types_from_provisioning()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        await _factory.PublishAsync(new TenantProvisioned
        {
            TenantId = tenantId, Code = "P" + tenantId, Name = "Trường " + tenantId, Subdomain = "p" + tenantId, TimeZone = "Asia/Ho_Chi_Minh", Modules = [],
        });

        await PatronApiFactory.WaitForAsync(() => Task.FromResult(_factory.PublishedOf<TenantSeeded>().FirstOrDefault(s => s.TenantId == tenantId)), "TenantSeeded");
        var seeded = Assert.Single(_factory.PublishedOf<TenantSeeded>(), s => s.TenantId == tenantId);
        Assert.True(seeded.Succeeded, seeded.Error);
        Assert.Equal("patron", seeded.Service);

        var types = await Read<IReadOnlyList<NamedItemDto>>(await Staff(tenantId).PostAsJsonAsync(U("/api/reader-types/SearchAll"), new CrudSearch(), Json));
        Assert.Equal(["Cán bộ, nhân viên", "Giáo viên", "Học sinh"], types.Select(t => t.Name));
    }

    [Fact]
    public async Task Reader_lifecycle_publishes_reader_changed_with_increasing_version()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var type = await AddNamed(staff, "reader-types", "Học sinh");
        var cls = await AddNamed(staff, "classes", "6A1");

        var created = await Read<ReaderDto>(await AddReader(staff, new ReaderRequest(" hs-001 ", "Nguyễn Văn", "An",
            BirthDate: new DateOnly(2014, 5, 2), Sex: 1, ReaderTypeId: type.Id, ClassId: cls.Id,
            IssueDate: new DateOnly(2026, 9, 5), ExpireDate: new DateOnly(2027, 6, 30), CardUid: "04:a1-b2 c3")));
        Assert.Equal("HS-001", created.CardNo);
        Assert.Equal("Nguyễn Văn An", created.FullName);
        Assert.Equal("04A1B2C3", created.CardUid);
        Assert.Equal(2, created.Status);

        var locked = await Read<ReaderDto>(await staff.PostAsJsonAsync(U($"/api/readers/Lock/{created.PublicId}"), new LockReaderRequest("Mất thẻ"), Json));
        Assert.Equal((1, "Mất thẻ"), (locked.Status, locked.LockReason));
        var unlocked = await Read<ReaderDto>(await staff.PostAsync(U($"/api/readers/Unlock/{created.PublicId}"), null));
        Assert.Equal((2, (string?)null), (unlocked.Status, unlocked.LockReason));

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/readers/Delete/{created.PublicId}"))).StatusCode);

        var events = _factory.PublishedOf<ReaderChanged>().Where(e => e.ReaderPublicId == created.PublicId).ToList();
        Assert.Equal(4, events.Count);
        Assert.All(events, e => Assert.Equal(tenantId, e.TenantId));
        Assert.Equal([2, 1, 2, 2], events.Select(e => e.Status));
        Assert.True(events.Select(e => e.Version).SequenceEqual(events.Select(e => e.Version).Order()) && events.Select(e => e.Version).Distinct().Count() == 4,
            "Version phải tăng dần qua từng thay đổi: " + string.Join(",", events.Select(e => e.Version)));
        Assert.True(events[^1].Deleted);
        Assert.Equal("Nguyễn Văn An", events[0].FullName);
    }

    [Fact]
    public async Task Card_number_and_uid_are_unique_per_tenant_and_references_must_exist()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("GV01", "Trần", "Bình", CardUid: "AA11")));

        var duplicate = await AddReader(staff, new ReaderRequest("gv01", null, "Khác"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("READER_CARDNO_EXISTS", await ErrorCode(duplicate));
        Assert.Equal("READER_CARDUID_EXISTS", await ErrorCode(await AddReader(staff, new ReaderRequest("GV02", null, "Khác", CardUid: "aa-11"))));
        Assert.Equal("READER_CARDNO_INVALID", await ErrorCode(await AddReader(staff, new ReaderRequest("GV 03", null, "Khác"))));
        Assert.Equal("READER_REF_NOT_FOUND", await ErrorCode(await AddReader(staff, new ReaderRequest("GV04", null, "Khác", ClassId: 999999))));
        Assert.Equal("READER_EXPIRE_BEFORE_ISSUE", await ErrorCode(await AddReader(staff, new ReaderRequest("GV05", null, "Khác",
            IssueDate: new DateOnly(2026, 9, 1), ExpireDate: new DateOnly(2026, 8, 1)))));

        Assert.True((await Read<CardNoCheck>(await staff.GetAsync(U("/api/readers/CheckExist?cardNo=gv01")))).Exists);
        Assert.False((await Read<CardNoCheck>(await staff.GetAsync(U("/api/readers/CheckExist?cardNo=GV99")))).Exists);

        // Đơn vị khác dùng lại được cùng số thẻ và không thấy bạn đọc của đơn vị này.
        var other = Staff(Interlocked.Increment(ref _nextTenantId));
        await Read<ReaderDto>(await AddReader(other, new ReaderRequest("GV01", null, "Người khác")));
        var otherPage = await Read<CrudPage<ReaderDto>>(await other.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { Keyword = "Bình" }, Json));
        Assert.Empty(otherPage.Items);
    }

    [Fact]
    public async Task Search_filters_by_keyword_class_and_expiry()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var a1 = await AddNamed(staff, "classes", "7A1");
        var a2 = await AddNamed(staff, "classes", "7A2");
        await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("S1", "Lê Thị", "Hoa", ClassId: a1.Id, Email: "hoa@truong.vn", ExpireDate: new DateOnly(2020, 1, 1))));
        await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("S2", "Phạm", "Minh", ClassId: a2.Id, Phone: "0912345678")));

        async Task<string[]> Find(ReaderSearch s) =>
            (await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), s, Json))).Items.Select(r => r.CardNo).Order().ToArray();

        Assert.Equal(["S1"], await Find(new ReaderSearch { Keyword = "thị hoa" }));
        Assert.Equal(["S1"], await Find(new ReaderSearch { Keyword = "HOA@TRUONG" }));
        Assert.Equal(["S2"], await Find(new ReaderSearch { Keyword = "0912" }));
        Assert.Equal(["S2"], await Find(new ReaderSearch { ClassId = a2.Id }));
        Assert.Equal(["S1"], await Find(new ReaderSearch { Expired = true }));
        Assert.Equal(["S2"], await Find(new ReaderSearch { Expired = false }));
        Assert.Equal(["S1", "S2"], await Find(new ReaderSearch { Keyword = "s" }));
    }

    [Fact]
    public async Task Bulk_update_changes_selected_readers_and_catalog_in_use_cannot_be_deleted()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var c6 = await AddNamed(staff, "classes", "6A");
        var c7 = await AddNamed(staff, "classes", "7A");
        var r1 = await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("B1", null, "Một", ClassId: c6.Id)));
        var r2 = await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("B2", null, "Hai", ClassId: c6.Id)));

        var result = await staff.PutAsJsonAsync(U("/api/readers/BulkUpdate"),
            new ReaderBulkUpdateRequest([r1.PublicId, r2.PublicId], ClassId: c7.Id, ExpireDate: new DateOnly(2027, 5, 31)), Json);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, JsonDocument.Parse(await result.Content.ReadAsStringAsync()).RootElement.GetProperty("updatedCount").GetInt32());

        var moved = await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { ClassId = c7.Id }, Json));
        Assert.Equal(2, moved.TotalCount);
        Assert.All(moved.Items, r => Assert.Equal(new DateOnly(2027, 5, 31), r.ExpireDate));
        Assert.Contains(_factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.EntityId == r2.PublicId.ToString()
            && true == a.ChangesJson?.Contains("\"classId\":[" + c6.Id + "," + c7.Id + "]", StringComparison.Ordinal));

        var delete = await staff.DeleteAsync(U($"/api/classes/Delete/{c7.PublicId}"));
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("CLASS_IN_USE", await ErrorCode(delete));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/classes/Delete/{c6.PublicId}"))).StatusCode);

        Assert.Equal("READER_BULK_EMPTY", await ErrorCode(await staff.PutAsJsonAsync(U("/api/readers/BulkUpdate"), new ReaderBulkUpdateRequest([r1.PublicId]), Json)));
    }

    [Fact]
    public async Task Reader_type_used_by_circulation_cannot_be_deleted()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var used = await AddNamed(staff, "reader-types", "Giáo viên");
        var down = await AddNamed(staff, "reader-types", "Phụ huynh");
        var free = await AddNamed(staff, "reader-types", "Khách");
        _factory.CirculationUsage[(tenantId, used.Id)] = new CirculationReaderTypeUsage(["GV — Quầy giáo viên"], 2);
        _factory.CirculationUsage[(tenantId, down.Id)] = null;

        var blocked = await staff.DeleteAsync(U($"/api/reader-types/Delete/{used.PublicId}"));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var detail = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString();
        Assert.Contains("điểm lưu thông GV — Quầy giáo viên; 2 chính sách lưu thông", detail, StringComparison.Ordinal);

        // Không hỏi được lưu thông thì không xoá (tránh để lại điểm/chính sách trỏ vào loại đã xoá).
        var unavailable = await staff.DeleteAsync(U($"/api/reader-types/Delete/{down.PublicId}"));
        Assert.Equal((HttpStatusCode.ServiceUnavailable, "CIRCULATION_UNAVAILABLE"), (unavailable.StatusCode, await ErrorCode(unavailable)));

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/reader-types/Delete/{free.PublicId}"))).StatusCode);
    }

    [Fact]
    public async Task Import_resolves_catalog_names_dates_and_full_name()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var hs = await AddNamed(staff, "reader-types", "Học sinh");
        var c = await AddNamed(staff, "classes", "8A3");

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        string[] header = ["Số thẻ", "Họ và tên", "Ngày sinh", "Giới tính", "Loại bạn đọc", "Lớp", "Ngày cấp thẻ"];
        for (var i = 0; i < header.Length; i++) sheet.Cell(1, i + 1).Value = header[i];
        sheet.Cell(2, 1).Value = "IM-01"; sheet.Cell(2, 2).Value = "Đỗ Minh Khang"; sheet.Cell(2, 3).Value = new DateTime(2013, 3, 9);
        sheet.Cell(2, 4).Value = "Nam"; sheet.Cell(2, 5).Value = "học sinh"; sheet.Cell(2, 6).Value = "8A3"; sheet.Cell(2, 7).Value = "05/09/2026";
        sheet.Cell(3, 1).Value = "IM-02"; sheet.Cell(3, 2).Value = "Hà"; sheet.Cell(3, 4).Value = "Nữ";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(stream.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue(CrudExcel.ContentType);
        form.Add(file, "file", "bandoc.xlsx");
        var result = await Read<CrudImportResult>(await staff.PostAsync(U("/api/readers/Import"), form));
        Assert.Equal(2, result.Imported);

        var page = await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { Keyword = "IM-" }, Json));
        var khang = Assert.Single(page.Items, r => r.CardNo == "IM-01");
        Assert.Equal(("Đỗ Minh", "Khang", 1), (khang.LastName, khang.FirstName, khang.Sex));
        Assert.Equal(new DateOnly(2013, 3, 9), khang.BirthDate);
        Assert.Equal((hs.Id, c.Id), (khang.ReaderTypeId, khang.ClassId));
        Assert.Equal((new DateOnly(2026, 9, 5), new DateOnly(2027, 9, 5)), (khang.IssueDate, khang.ExpireDate)); // mặc định hạn 1 năm
        var ha = Assert.Single(page.Items, r => r.CardNo == "IM-02");
        Assert.Equal(((string?)null, "Hà", 0), (ha.LastName, ha.FirstName, ha.Sex));

        // Lớp chưa có trong danh mục → báo đúng dòng, không nhập gì.
        sheet.Cell(2, 1).Value = "IM-03"; sheet.Cell(2, 6).Value = "9Z9"; sheet.Cell(3, 1).Value = "IM-04";
        using var bad = new MemoryStream();
        workbook.SaveAs(bad);
        var badForm = new MultipartFormDataContent();
        badForm.Add(new ByteArrayContent(bad.ToArray()), "file", "bandoc.xlsx");
        var rejected = await staff.PostAsync(U("/api/readers/Import"), badForm);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var error = Assert.Single((await rejected.Content.ReadFromJsonAsync<CrudImportResult>(Json))!.Errors);
        Assert.Equal(2, error.Row);
        Assert.Contains("9Z9", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_writes_selected_fields_with_catalog_names_and_reimports_into_another_tenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var c = await AddNamed(staff, "classes", "9B");
        await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("007", "Vũ", "Lan", Sex: 0, ClassId: c.Id,
            BirthDate: new DateOnly(2012, 1, 20), IssueDate: new DateOnly(2026, 9, 1), ExpireDate: new DateOnly(2027, 8, 31))));
        await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("008", null, "Tùng")));

        var fields = await Read<IReadOnlyList<CrudExportField>>(await staff.GetAsync(U("/api/readers/GetExportFields")));
        Assert.Contains(fields, f => f is { Code: "class", Name: "Lớp" });

        var response = await staff.PostAsJsonAsync(U("/api/readers/Export"),
            new ReaderExportRequest(new ReaderSearch { ClassId = c.Id }, ["cardno", "lastname", "firstname", "sex", "class", "birthdate", "issuedate", "expiredate"]), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using (var workbook = new XLWorkbook(new MemoryStream(bytes)))
        {
            var sheet = workbook.Worksheet(1);
            // Thứ tự cột theo khai báo, không theo thứ tự gửi lên.
            Assert.Equal(["STT", "Số thẻ", "Họ đệm", "Tên", "Ngày sinh", "Giới tính", "Lớp", "Ngày cấp thẻ", "Ngày hết hạn"],
                Enumerable.Range(1, 9).Select(i => sheet.Cell(1, i).GetString()));
            Assert.Equal(["1", "007", "Vũ", "Lan", "20/01/2012", "Nữ", "9B"], Enumerable.Range(1, 7).Select(i => sheet.Cell(2, i).GetFormattedString()));
            Assert.Equal(new DateTime(2012, 1, 20), sheet.Cell(2, 5).GetDateTime());
            Assert.True(sheet.Cell(3, 2).IsEmpty(), "chỉ xuất bạn đọc lớp 9B");
        }

        Assert.Equal("EXPORT_FIELD_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/readers/Export"), new ReaderExportRequest(null, ["password"]), Json)));

        // File xuất nhập lại được (đơn vị khác có lớp cùng tên): số thẻ giữ số 0 đầu, ngày và giới tính đọc đúng.
        var other = Staff(Interlocked.Increment(ref _nextTenantId));
        await AddNamed(other, "classes", "9B");
        var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "export.xlsx" } };
        Assert.Equal(1, (await Read<CrudImportResult>(await other.PostAsync(U("/api/readers/Import"), form))).Imported);
        var lan = Assert.Single((await Read<CrudPage<ReaderDto>>(await other.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch(), Json))).Items);
        Assert.Equal(("007", "Vũ Lan", 0, new DateOnly(2012, 1, 20), new DateOnly(2027, 8, 31)),
            (lan.CardNo, lan.FullName, lan.Sex, lan.BirthDate, lan.ExpireDate));
    }

    [Fact]
    public async Task Lrc_style_import_resolves_tenant_catalogs_and_new_filters_find_readers()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        _factory.References[(tenantId, "orgs")] = [new(31, "Khoa Toán"), new(32, "Bộ môn Đại số", 31), new(33, "Văn phòng"), new(34, "Văn phòng", 31)];
        _factory.References[(tenantId, "ethnicities")] = [new(1, "Kinh"), new(2, "Tày")];
        _factory.References[(tenantId, "degrees")] = [new(5, "Đại học")];
        _factory.References[(tenantId, "academic-titles")] = [new(7, "Tiến sĩ")];

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Data");
        // Tiêu đề như file của ELIB-LRC (không dấu, "Don vi", "Dan toc"…); giới tính LRC: 2 = Nữ.
        string[] header = ["Ma the", "Ho", "Ten", "Gioi tinh", "Don vi", "Dan toc", "Trinh do", "Hoc ham"];
        for (var i = 0; i < header.Length; i++) sheet.Cell(1, i + 1).Value = header[i];
        object[][] rows =
        [
            ["SV-0101", "Nguyễn Thị", "Hoa", "2", "khoa  toán", "Tày", "Đại học", ""],
            ["SV-0102", "Trần", "Bình", "1", "32", "Kinh", "", "Tiến sĩ"],
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var col = 0; col < rows[r].Length; col++) sheet.Cell(r + 2, col + 1).Value = rows[r][col].ToString();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var form = new MultipartFormDataContent { { new ByteArrayContent(stream.ToArray()), "file", "lrc.xlsx" } };
        Assert.Equal(2, (await Read<CrudImportResult>(await staff.PostAsync(U("/api/readers/Import"), form))).Imported);

        var all = (await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch(), Json))).Items;
        var hoa = Assert.Single(all, r => r.CardNo == "SV-0101");
        Assert.Equal((0, 31L, 2L, 5L, (long?)null), (hoa.Sex, hoa.OrgId, hoa.EthnicityId, hoa.DegreeId, hoa.AcademicTitleId));
        var binh = Assert.Single(all, r => r.CardNo == "SV-0102");
        Assert.Equal((32L, 1L, 7L), (binh.OrgId, binh.EthnicityId, binh.AcademicTitleId));

        // Tên đơn vị trùng ở hai nhánh → báo dòng, gợi ý ghi số id.
        sheet.Cell(2, 1).Value = "SV-0103"; sheet.Cell(2, 5).Value = "Văn phòng"; sheet.Cell(3, 1).Value = "SV-0104";
        using var ambiguous = new MemoryStream();
        workbook.SaveAs(ambiguous);
        var rejected = await staff.PostAsync(U("/api/readers/Import"), new MultipartFormDataContent { { new ByteArrayContent(ambiguous.ToArray()), "file", "lrc.xlsx" } });
        var error = Assert.Single((await rejected.Content.ReadFromJsonAsync<CrudImportResult>(Json))!.Errors);
        Assert.Contains("số id", error.Message, StringComparison.Ordinal);

        // Bộ lọc kiểu ELIB-LRC: Họ / Tên / Mã thẻ chứa, nhiều đơn vị (đơn vị + đơn vị con do app gửi).
        async Task<string[]> Find(ReaderSearch s) =>
            [.. (await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), s, Json))).Items.Select(r => r.CardNo).Order()];
        Assert.Equal(["SV-0101"], await Find(new ReaderSearch { LastName = "nguyễn" }));
        Assert.Equal(["SV-0102"], await Find(new ReaderSearch { FirstName = "BÌNH" }));
        Assert.Equal(["SV-0101", "SV-0102"], await Find(new ReaderSearch { CardNoContains = "sv-01" }));
        Assert.Equal(["SV-0101", "SV-0102"], await Find(new ReaderSearch { OrgIds = [31, 32] }));
        Assert.Equal(["SV-0102"], await Find(new ReaderSearch { OrgIds = [32] }));
        Assert.Equal(["SV-0101"], await Find(new ReaderSearch { PublicIds = [hoa.PublicId, Guid.NewGuid()] }));

        // Xuất kèm tên đơn vị, dân tộc, trình độ, học hàm.
        var export = await staff.PostAsJsonAsync(U("/api/readers/Export"),
            new ReaderExportRequest(new ReaderSearch { CardNo = "SV-0102" }, ["cardno", "org", "ethnicity", "degree", "academictitle"]), Json);
        using var exported = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
        Assert.Equal(["SV-0102", "Bộ môn Đại số", "Kinh", "", "Tiến sĩ"], Enumerable.Range(2, 5).Select(i => exported.Worksheet(1).Cell(2, i).GetString()));
    }

    [Fact]
    public async Task Advanced_import_maps_columns_previews_runs_in_background_and_returns_error_file()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        _factory.References[(tenantId, "orgs")] = [new(41, "Khoa Lý")];
        await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("IM-001", "Cũ", "An", Phone: "0900000001", Address: "Giữ nguyên",
            IssueDate: new DateOnly(2026, 1, 1), ExpireDate: new DateOnly(2027, 1, 1))));

        // File kiểu ELIB-LRC: 2 dòng tiêu đề phụ phía trên, cột tự đặt tên → ghép cột thủ công cho cột lạ.
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("DS");
        sheet.Cell(1, 1).Value = "DANH SÁCH BẠN ĐỌC";
        string[] header = ["STT", "Ma the", "Ho dem", "Ten", "Ngay sinh", "Lop hoc", "Khoa hoc", "Don vi", "Dien thoai"];
        for (var i = 0; i < header.Length; i++) sheet.Cell(3, i + 1).Value = header[i];
        object[][] rows =
        [
            ["1", "IM-001", "", "An Mới", "", "", "", "Khoa Lý", ""],      // ghi đè: ô trống giữ cũ
            ["2", "IM-002", "Lê", "Bình", "20100305", "10A9", "K-2026", "", ""], // lớp/khoá mới → tự tạo; ngày yyyyMMdd
            ["3", "IM-003", "Phạm", "Chi", "31/02/2010", "", "", "", ""],   // ngày sai
            ["4", "IM-002", "Lê", "Bình 2", "", "", "", "", ""],            // trùng trong file
            ["5", "IM-005", "Hồ", "Duy", "", "", "", "Khoa Hoá", ""],       // đơn vị không có
        ];
        for (var r = 0; r < rows.Length; r++)
            for (var col = 0; col < rows[r].Length; col++) sheet.Cell(r + 4, col + 1).Value = rows[r][col].ToString();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();

        MultipartFormDataContent FileForm(params (string Name, string Value)[] fields)
        {
            var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "lrc-ds.xlsx" } };
            foreach (var (name, value) in fields) form.Add(new StringContent(value), name);
            return form;
        }

        var columns = await Read<ImportColumnsDto>(await staff.PostAsync(U("/api/readers/Import/Columns"), FileForm()));
        Assert.Equal(3, columns.HeaderRow);
        Assert.Equal(5, columns.DataRows);
        Assert.Equal((2, 3, 4, 5, 8, 9), (columns.Suggested["cardno"], columns.Suggested["lastname"], columns.Suggested["firstname"],
            columns.Suggested["birthdate"], columns.Suggested["org"], columns.Suggested["phone"]));
        Assert.False(columns.Suggested.ContainsKey("class"));

        var mapping = new Dictionary<string, int>(columns.Suggested) { ["class"] = 6, ["course"] = 7 };
        async Task<ReaderImportJobDto> Preview(bool overwrite, bool autoCreate) =>
            await Read<ReaderImportJobDto>(await staff.PostAsync(U("/api/readers/Import/Jobs"), FileForm(("options",
                JsonSerializer.Serialize(new ReaderImportOptions(columns.HeaderRow, mapping, overwrite, null, autoCreate, OpacCardNoPassword: true), Json)))));

        var strict = await Preview(overwrite: false, autoCreate: false);
        Assert.Equal((5, 0, 0, 5), (strict.Total, strict.ToCreate, strict.ToUpdate, strict.ErrorCount));
        Assert.Contains(strict.Errors, e => e.Row == 4 && e.Message.Contains("Ghi đè", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsync(U($"/api/readers/Import/Jobs/{strict.Id}/Start"), null)).StatusCode);

        var job = await Preview(overwrite: true, autoCreate: true);
        Assert.Equal(("Ready", 5, 1, 1, 3), (job.Status, job.Total, job.ToCreate, job.ToUpdate, job.ErrorCount));
        Assert.Equal([6, 7, 8], job.Errors.Select(e => e.Row));
        Assert.True(job.HasErrorFile);
        Assert.Empty((await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { CardNo = "IM-002" }, Json))).Items);

        // Đơn vị khác không thấy lượt nhập.
        Assert.Equal(HttpStatusCode.NotFound, (await Staff(tenantId + 1000).GetAsync(U($"/api/readers/Import/Jobs/{job.Id}"))).StatusCode);

        Assert.Equal("Queued", (await Read<ReaderImportJobDto>(await staff.PostAsync(U($"/api/readers/Import/Jobs/{job.Id}/Start"), null))).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsync(U($"/api/readers/Import/Jobs/{job.Id}/Start"), null)).StatusCode);
        var done = await PatronApiFactory.WaitForAsync(async () =>
        {
            var current = await Read<ReaderImportJobDto>(await staff.GetAsync(U($"/api/readers/Import/Jobs/{job.Id}")));
            return current.Status is "Done" or "Failed" ? current : null;
        }, "lượt nhập chạy xong");
        Assert.Equal(("Done", 5, 1, 1, 3), (done.Status, done.Processed, done.Created, done.Updated, done.Failed));

        var all = (await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch(), Json))).Items;
        var an = Assert.Single(all, r => r.CardNo == "IM-001");
        Assert.Equal(("Cũ", "An Mới", "0900000001", "Giữ nguyên", 41L, new DateOnly(2027, 1, 1)),
            (an.LastName, an.FirstName, an.Phone, an.Address, an.OrgId, an.ExpireDate));
        var binh = Assert.Single(all, r => r.CardNo == "IM-002");
        Assert.Equal(new DateOnly(2010, 3, 5), binh.BirthDate);
        Assert.NotNull(binh.ClassId);
        Assert.NotNull(binh.CourseId);
        Assert.Equal(binh.IssueDate!.Value.AddYears(1), binh.ExpireDate);
        // Mật khẩu OPAC = số thẻ chỉ cho bạn đọc mới (identity đặt theo cờ trong event); bạn đọc cập nhật giữ mật khẩu cũ.
        Assert.Contains(_factory.PublishedOf<ReaderChanged>(), e => e.TenantId == tenantId && e.CardNo == "IM-002" && e.InitialCardNoPassword);
        Assert.DoesNotContain(_factory.PublishedOf<ReaderChanged>(), e => e.TenantId == tenantId && e.CardNo == "IM-001" && e.InitialCardNoPassword);
        Assert.Contains(_factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.Action == "IMPORT" && a.Actor?.Kind == "staff"
            && (a.Summary ?? "").Contains("mật khẩu OPAC", StringComparison.Ordinal));

        // File "Dòng cần sửa": số dòng gốc + cột gốc + cột Lỗi, đúng các dòng lỗi.
        var errors = await staff.GetAsync(U($"/api/readers/Import/Jobs/{job.Id}/Errors"));
        Assert.Equal(HttpStatusCode.OK, errors.StatusCode);
        using var errorBook = new XLWorkbook(new MemoryStream(await errors.Content.ReadAsByteArrayAsync()));
        var errorSheet = errorBook.Worksheet(1);
        Assert.Equal(("Dòng", "Ma the"), (errorSheet.Cell(1, 1).GetString(), errorSheet.Cell(1, 3).GetString()));
        Assert.Equal(["6IM-003", "7IM-002", "8IM-005"], Enumerable.Range(2, 3).Select(r => errorSheet.Cell(r, 1).GetString() + errorSheet.Cell(r, 3).GetString()));
        Assert.Contains("Khoa Hoá", errorSheet.Row(4).CellsUsed().Last().GetString(), StringComparison.Ordinal);

        // Lớp/Khoá/Đơn vị "theo mã" (ELIB-LRC): ô là số id có sẵn — tên hoặc id không có đều báo lỗi, không tự tạo.
        using var byCodeBook = new XLWorkbook();
        var byCodeSheet = byCodeBook.AddWorksheet("DS");
        object[][] byCodeRows =
        [
            ["Ma the", "Ten", "Lop", "Khoa", "Don vi"],
            ["IM-010", "Giang", binh.ClassId!.Value, binh.CourseId!.Value, 41],
            ["IM-011", "Hà", "10A9", "", ""],     // tên lớp khi chọn theo mã
            ["IM-012", "Khánh", "", "", "Khoa Lý"], // tên đơn vị khi chọn theo mã
            ["IM-013", "Lan", 999999, "", ""],    // id không có
        ];
        for (var r = 0; r < byCodeRows.Length; r++)
            for (var col = 0; col < byCodeRows[r].Length; col++) byCodeSheet.Cell(r + 1, col + 1).Value = byCodeRows[r][col].ToString();
        using var byCodeStream = new MemoryStream();
        byCodeBook.SaveAs(byCodeStream);
        var byCodeOptions = new ReaderImportOptions(1, new Dictionary<string, int> { ["cardno"] = 1, ["firstname"] = 2, ["class"] = 3, ["course"] = 4, ["org"] = 5 },
            AutoCreateRefs: true, ClassByCode: true, CourseByCode: true, OrgByCode: true);
        var byCode = await Read<ReaderImportJobDto>(await staff.PostAsync(U("/api/readers/Import/Jobs"), new MultipartFormDataContent
        {
            { new ByteArrayContent(byCodeStream.ToArray()), "file", "theo-ma.xlsx" },
            { new StringContent(JsonSerializer.Serialize(byCodeOptions, Json)), "options" },
        }));
        Assert.Equal((4, 1, 3), (byCode.Total, byCode.ToCreate, byCode.ErrorCount));
        Assert.Equal([3, 4, 5], byCode.Errors.Select(e => e.Row));
        Assert.All(byCode.Errors, e => Assert.Contains("mã", e.Message, StringComparison.Ordinal));
        await staff.PostAsync(U($"/api/readers/Import/Jobs/{byCode.Id}/Start"), null);
        var byCodeDone = await PatronApiFactory.WaitForAsync(async () =>
        {
            var current = await Read<ReaderImportJobDto>(await staff.GetAsync(U($"/api/readers/Import/Jobs/{byCode.Id}")));
            return current.Status is "Done" or "Failed" ? current : null;
        }, "lượt nhập theo mã chạy xong");
        Assert.Equal(("Done", 1), (byCodeDone.Status, byCodeDone.Created));
        var giang = Assert.Single((await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { CardNo = "IM-010" }, Json))).Items);
        Assert.Equal((binh.ClassId, binh.CourseId, (long?)41), (giang.ClassId, giang.CourseId, giang.OrgId));

        // Không có quyền thêm bạn đọc → không xem được cột/lượt nhập.
        var viewer = Staff(tenantId, "READERS:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(U($"/api/readers/Import/Jobs/{job.Id}"))).StatusCode);
    }

    [Fact]
    public async Task Platform_admin_counts_readers_per_tenant_and_tenant_staff_cannot()
    {
        var a = Interlocked.Increment(ref _nextTenantId);
        var b = Interlocked.Increment(ref _nextTenantId);
        var staffA = Staff(a);
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(-1);
        await Read<ReaderDto>(await AddReader(staffA, new ReaderRequest("TK-01", null, "An")));
        await Read<ReaderDto>(await AddReader(staffA, new ReaderRequest("TK-02", null, "Bình", IssueDate: yesterday.AddYears(-1), ExpireDate: yesterday)));
        var locked = await Read<ReaderDto>(await AddReader(staffA, new ReaderRequest("TK-03", null, "Chi")));
        await Read<ReaderDto>(await staffA.PostAsJsonAsync(U($"/api/readers/Lock/{locked.PublicId}"), new LockReaderRequest("Mất thẻ"), Json));
        await Read<ReaderDto>(await AddReader(Staff(b), new ReaderRequest("TK-01", null, "Dũng")));

        var system = _factory.CreateClient().WithClaims(TestClaims.SuperAdmin);
        async Task<List<ReaderPlatformStats>> Stats(PlatformStatsRequest request) =>
            await Read<List<ReaderPlatformStats>>(await system.PostAsJsonAsync(U("/api/system/stats/readers"), request, Json));

        var today = yesterday.AddDays(1);
        var now = await Stats(new PlatformStatsRequest([a, b], today, today));
        Assert.Equal([new ReaderPlatformStats(a, 3, 1, 1, 3), new ReaderPlatformStats(b, 1, 0, 0, 1)], now);
        // Kỳ trước hôm nay: không có bạn đọc mới, số hiện tại giữ nguyên.
        Assert.Equal(new ReaderPlatformStats(a, 3, 1, 1, 0), Assert.Single(await Stats(new PlatformStatsRequest([a], null, yesterday))));

        Assert.Equal(HttpStatusCode.Forbidden, (await staffA.PostAsJsonAsync(U("/api/system/stats/readers"), new PlatformStatsRequest(), Json)).StatusCode);
    }

    [Fact]
    public async Task Extra_face_photos_are_limited_unique_and_private_to_the_tenant()
    {
        var staff = Staff(Interlocked.Increment(ref _nextTenantId));
        var reader = await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("XP-1", null, "Ảnh")));
        var first = Guid.NewGuid();
        await staff.PostAsJsonAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}"), new ReaderExtraPhotoRequest(first), Json);
        Assert.Equal("PHOTO_EXISTS", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}"), new ReaderExtraPhotoRequest(first), Json)));
        for (var i = 1; i < Elib.Patron.Domain.ReaderPhoto.MaxPerReader; i++)
            Assert.True((await staff.PostAsJsonAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}"), new ReaderExtraPhotoRequest(Guid.NewGuid()), Json)).IsSuccessStatusCode);
        Assert.Equal("PHOTO_LIMIT", await ErrorCode(await staff.PostAsJsonAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}"), new ReaderExtraPhotoRequest(Guid.NewGuid()), Json)));

        var listed = await Read<List<ReaderExtraPhotoDto>>(await staff.GetAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}")));
        Assert.Equal(Elib.Patron.Domain.ReaderPhoto.MaxPerReader, listed.Count);

        var other = Staff(Interlocked.Increment(ref _nextTenantId));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}/{first}"))).StatusCode);
        Assert.Equal(Elib.Patron.Domain.ReaderPhoto.MaxPerReader - 1, (await Read<List<ReaderExtraPhotoDto>>(await staff.GetAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}")))).Count);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.DeleteAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}/{first}"))).StatusCode);
    }

    private static readonly string CameraJpeg = Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4]);

    private async Task<(ReaderDto Reader, byte[] Photo)> ReaderWithPhoto(HttpClient staff, string card, byte seed, bool extra = false)
    {
        var reader = await Read<ReaderDto>(await AddReader(staff, new ReaderRequest(card, null, "Mặt " + card)));
        var photo = new byte[] { 0xFF, 0xD8, 0xFF, seed, seed, seed };
        var file = Guid.NewGuid();
        _factory.PhotoFiles[file] = photo;
        if (extra) await staff.PostAsJsonAsync(U($"/api/readers/ExtraPhotos/{reader.PublicId}"), new ReaderExtraPhotoRequest(file), Json);
        else await staff.PutAsJsonAsync(U($"/api/readers/Photo/{reader.PublicId}"), new ReaderPhotoRequest(file), Json);
        return (reader, photo);
    }

    [Fact]
    public async Task Face_identify_matches_main_and_extra_photos_applies_the_threshold_and_never_reads_other_tenants_photos()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        Assert.True((await Read<FaceStatus>(await staff.GetAsync(U("/api/readers/FaceStatus")))).Enabled);

        Assert.Null(await Read<FaceMatch?>(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(CameraJpeg), Json))); // chưa có ảnh nào

        var (a, _) = await ReaderWithPhoto(staff, "FA-1", 10);
        var (b, bExtra) = await ReaderWithPhoto(staff, "FA-2", 20, extra: true);
        var other = Staff(Interlocked.Increment(ref _nextTenantId));
        await ReaderWithPhoto(other, "FA-9", 30);
        _factory.PhotoReads.Clear();

        _factory.Matcher.Target = bExtra;
        var hit = await Read<FaceMatch?>(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest("data:image/jpeg;base64," + CameraJpeg), Json));
        Assert.Equal((b.PublicId, "FA-2"), (hit!.ReaderPublicId, hit.CardNo));
        Assert.Equal(2, _factory.PhotoReads.Count); // chỉ ảnh của hai bạn đọc cùng đơn vị

        _factory.Matcher.Confidence = 0.5; // dưới ngưỡng 0.7
        Assert.Null(await Read<FaceMatch?>(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(CameraJpeg), Json)));
        _factory.Matcher.Confidence = 0.9;
        _factory.Matcher.Target = null; // không ai khớp
        Assert.Null(await Read<FaceMatch?>(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(CameraJpeg), Json)));

        _factory.Matcher.Fail = true; // mọi lô lỗi → báo lỗi dịch vụ, không phải "không nhận ra"
        try
        {
            Assert.Equal("FACE_UNAVAILABLE", await ErrorCode(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(CameraJpeg), Json)));
        }
        finally
        {
            _factory.Matcher.Fail = false;
        }
        Assert.NotEqual(a.PublicId, hit.ReaderPublicId);
    }

    [Fact]
    public async Task Face_identify_reports_an_error_when_no_photo_can_be_read_and_splits_big_photos_into_batches()
    {
        var staff = Staff(Interlocked.Increment(ref _nextTenantId));
        var (_, p1) = await ReaderWithPhoto(staff, "FB-1", 1);
        var (_, p2) = await ReaderWithPhoto(staff, "FB-2", 2);
        _factory.PhotoSourceDown = true;
        try
        {
            Assert.Equal("FACE_UNAVAILABLE", await ErrorCode(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(CameraJpeg), Json)));
        }
        finally
        {
            _factory.PhotoSourceDown = false;
        }

        // Hai ảnh 5 MB: tổng quá trần lô (8 MB) → mỗi ảnh một lô, không dồn vào một yêu cầu Gemini.
        foreach (var file in _factory.PhotoFiles.Keys.ToList())
            if (_factory.PhotoFiles[file] is var small && (small.SequenceEqual(p1) || small.SequenceEqual(p2)))
                _factory.PhotoFiles[file] = [.. small, .. new byte[5_000_000]];
        _factory.Matcher.Batches.Clear();
        _factory.Matcher.Target = null;
        await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(CameraJpeg), Json);
        Assert.Equal(2, _factory.Matcher.Batches.Count(b => b.Any(x => x.Length > 4_000_000)));
        Assert.All(_factory.Matcher.Batches, b => Assert.True(b.Count == 1 || b.Sum(x => (long)x.Length) <= 8_000_000));
    }

    [Fact]
    public async Task Face_identify_rejects_bad_images_and_is_off_without_a_key()
    {
        var staff = Staff(Interlocked.Increment(ref _nextTenantId));
        Assert.Equal("FACE_IMAGE_REQUIRED", await ErrorCode(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(" "), Json)));
        Assert.Equal("FACE_IMAGE_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest("không phải base64"), Json)));
        Assert.Equal("FACE_IMAGE_INVALID", await ErrorCode(await staff.PostAsJsonAsync(U("/api/readers/FaceIdentify"), new FaceIdentifyRequest(Convert.ToBase64String([1, 2, 3, 4, 5])), Json)));

        using var off = _factory.WithWebHostBuilder(b => b.UseSetting("FaceRecognition:ApiKey", ""));
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = ["*"];
        var client = off.CreateClient().WithClaims(TestClaims.Staff(Interlocked.Increment(ref _nextTenantId), userId));
        Assert.False((await Read<FaceStatus>(await client.GetAsync(U("/api/readers/FaceStatus")))).Enabled);
    }

    [Fact]
    public async Task Photos_are_set_per_reader_and_in_bulk_by_card_number()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var r1 = await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("PH-1", null, "Một")));
        await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("PH-2", null, "Hai")));

        var photo = Guid.NewGuid();
        var set = await Read<ReaderDto>(await staff.PutAsJsonAsync(U($"/api/readers/Photo/{r1.PublicId}"), new ReaderPhotoRequest(photo), Json));
        Assert.Equal(photo, set.PhotoId);
        var before = _factory.PublishedOf<ReaderChanged>().Count(e => e.ReaderPublicId == r1.PublicId);

        var (p1, p2) = (Guid.NewGuid(), Guid.NewGuid());
        var result = await Read<ReaderPhotosResult>(await staff.PostAsJsonAsync(U("/api/readers/Photos"),
            new ReaderPhotosRequest([new(" ph-1 ", p1), new("PH-2", p2), new("PH-404", Guid.NewGuid())]), Json));
        Assert.Equal(2, result.Matched);
        Assert.Equal(["PH-404"], result.NotFound);

        var page = await Read<CrudPage<ReaderDto>>(await staff.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch { Keyword = "PH-" }, Json));
        Assert.Equal(["PH-1", "PH-2"], page.Items.OrderBy(r => r.CardNo).Select(r => r.CardNo));
        Assert.Equal([p1, p2], page.Items.OrderBy(r => r.CardNo).Select(r => r.PhotoId!.Value));
        // Ảnh có trong bản sao bạn đọc (màn mượn trả) → phát ReaderChanged kèm PhotoId.
        Assert.Equal(before + 1, _factory.PublishedOf<ReaderChanged>().Count(e => e.ReaderPublicId == r1.PublicId));
        Assert.Equal(p1, _factory.PublishedOf<ReaderChanged>().Where(e => e.ReaderPublicId == r1.PublicId).MaxBy(e => e.Version)!.PhotoId);

        // Đơn vị khác không gán được ảnh cho bạn đọc của đơn vị này.
        var other = Staff(Interlocked.Increment(ref _nextTenantId));
        var foreign = await Read<ReaderPhotosResult>(await other.PostAsJsonAsync(U("/api/readers/Photos"), new ReaderPhotosRequest([new("PH-1", Guid.NewGuid())]), Json));
        Assert.Equal(0, foreign.Matched);

        var cleared = await Read<ReaderDto>(await staff.PutAsJsonAsync(U($"/api/readers/Photo/{r1.PublicId}"), new ReaderPhotoRequest(null), Json));
        Assert.Null(cleared.PhotoId);
    }

    [Fact]
    public async Task Permissions_follow_monolith_module_codes()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var viewer = Staff(tenantId, "READERS:view");
        Assert.Equal(HttpStatusCode.OK, (await viewer.PostAsJsonAsync(U("/api/readers/Search"), new ReaderSearch(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AddReader(viewer, new ReaderRequest("X1", null, "X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(U("/api/reader-types/Search"), new CrudSearch(), Json)).StatusCode);

        var typesOnly = Staff(tenantId, "READER_TYPES:view", "READER_TYPES:add");
        Assert.Equal(HttpStatusCode.Created, (await typesOnly.PostAsJsonAsync(U("/api/reader-types/Add"), new NameRequest("Phụ huynh"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await typesOnly.PostAsJsonAsync(U("/api/readers/Lock/" + Guid.NewGuid()), new LockReaderRequest(null), Json)).StatusCode);
    }

    [Fact]
    public async Task Reader_event_carries_catalog_names_and_internal_state_is_for_services_only()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        var staff = Staff(tenantId);
        var type = await AddNamed(staff, "reader-types", "Học sinh");
        var cls = await AddNamed(staff, "classes", "6A1");
        var created = await Read<ReaderDto>(await AddReader(staff, new ReaderRequest("hs-77", "Phạm", "Minh", ReaderTypeId: type.Id, ClassId: cls.Id)));

        var e = _factory.PublishedOf<ReaderChanged>().Single(x => x.ReaderPublicId == created.PublicId);
        Assert.Equal(("Học sinh", "6A1", (string?)null), (e.ReaderTypeName, e.ClassName, e.CourseName));

        var service = _factory.CreateClient().WithClaims(TestClaims.Service);
        var state = await Read<ReaderChanged>(await service.GetAsync(U($"/internal/tenants/{tenantId}/readers/by-card/hs-77")));
        Assert.Equal((created.PublicId, "HS-77", "Phạm Minh", "6A1", tenantId), (state.ReaderPublicId, state.CardNo, state.FullName, state.ClassName, state.TenantId));
        Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync(U($"/internal/tenants/{tenantId}/readers/by-card/KHONGCO"))).StatusCode);
        var byId = await Read<ReaderChanged>(await service.GetAsync(U($"/internal/tenants/{tenantId}/readers/{created.PublicId}")));
        Assert.Equal(("HS-77", tenantId), (byId.CardNo, byId.TenantId));
        Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync(U($"/internal/tenants/{tenantId}/readers/{Guid.NewGuid()}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await service.GetAsync(U($"/internal/tenants/{tenantId + 1}/readers/{created.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenantId}/readers/by-card/HS-77"))).StatusCode);
    }
}
