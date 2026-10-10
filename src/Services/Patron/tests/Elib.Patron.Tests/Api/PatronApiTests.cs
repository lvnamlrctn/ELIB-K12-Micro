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
        Assert.Contains(_factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.Summary!.Contains("hàng loạt 2", StringComparison.Ordinal));

        var delete = await staff.DeleteAsync(U($"/api/classes/Delete/{c7.PublicId}"));
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal("CLASS_IN_USE", await ErrorCode(delete));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/classes/Delete/{c6.PublicId}"))).StatusCode);

        Assert.Equal("READER_BULK_EMPTY", await ErrorCode(await staff.PutAsJsonAsync(U("/api/readers/BulkUpdate"), new ReaderBulkUpdateRequest([r1.PublicId]), Json)));
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
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenantId}/readers/by-card/HS-77"))).StatusCode);
    }
}
