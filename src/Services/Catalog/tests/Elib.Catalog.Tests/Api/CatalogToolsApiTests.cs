using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Testing;
using Elib.Catalog.Application;
using Elib.Catalog.Domain;
using Elib.Contracts.Events.Platform;

namespace Elib.Catalog.Tests.Api;

/// <summary>Từ điển biên mục (tự học + gợi ý + quyền theo loại), cấu hình ISBD theo loại biểu ghi, lịch sử biên mục theo trường MARC.</summary>
public sealed class CatalogToolsApiTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextTenantId = 7500;
    private static long _nextUserId = 17500;

    public Task InitializeAsync() => factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private long NewTenant()
    {
        var tenantId = Interlocked.Increment(ref _nextTenantId);
        factory.Licenses.Licensed.Add((tenantId, "CATALOG"));
        return tenantId;
    }

    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static MarcField Data(string tag, string ind1, string ind2, params (string Code, string Value)[] subfields) =>
        new(tag, ind1, ind2, Subfields: [.. subfields.Select(s => new MarcSubfield(s.Code, s.Value))]);

    private static BibRequest Book(long? typeId, string title, string author, string publisher, params string[] keywords) => new(typeId,
    [
        Data("082", "0", "4", ("a", "895.922")),
        Data("100", "0", " ", ("a", author + ",")),
        Data("245", "1", "0", ("a", title + " /"), ("c", author)),
        Data("264", " ", "1", ("a", "H. :"), ("b", publisher + ","), ("c", "2020")),
        .. keywords.Select(k => Data("650", " ", "7", ("a", k))),
    ]);

    private static async Task<List<DictionarySuggestion>> Suggest(HttpClient staff, string kind, string q) =>
        await Read<List<DictionarySuggestion>>(await staff.GetAsync(U($"/api/dictionary/Suggest?kind={kind}&q={Uri.EscapeDataString(q)}")));

    [Fact]
    public async Task Dictionaries_learn_from_saved_bibs_and_suggest_by_prefix_without_diacritics()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var b1 = await Read<BibDto>(await staff.PostAsJsonAsync(U("/api/bibs/Add"), Book(null, "Dế mèn phiêu lưu ký", "Tô Hoài", "Kim Đồng", "Văn học thiếu nhi"), Json));
        await Read<BibDto>(await staff.PostAsJsonAsync(U("/api/bibs/Add"), Book(null, "Vợ chồng A Phủ", "Tô Hoài", "Kim Đồng.", "Truyện ngắn"), Json));

        var authors = await Suggest(staff, "author", "to ho");
        Assert.Equal("Tô Hoài", Assert.Single(authors).Value);
        Assert.Equal("Kim Đồng", Assert.Single(await Suggest(staff, "publisher", "KIM")).Value); // dấu câu cuối bị bỏ, không trùng
        Assert.Equal(["Truyện ngắn", "Văn học thiếu nhi"], (await Suggest(staff, "keyword", "n")).Select(s => s.Value).Order());

        var list = await Read<CrudPage<DictionaryDto>>(await staff.PostAsJsonAsync(U("/api/dic-authors/Search"), new CrudSearch(), Json));
        Assert.Equal((2, true), (Assert.Single(list.Items).UseCount, list.Items[0].AutoAdded));

        // Sửa biểu ghi: chỉ giá trị mới xuất hiện được học.
        var edit = Book(null, "Dế mèn phiêu lưu ký", "Tô Hoài", "Kim Đồng", "Văn học thiếu nhi", "Phiêu lưu");
        await Read<BibDto>(await staff.PutAsJsonAsync(U($"/api/bibs/Update/{b1.PublicId}"), edit, Json));
        Assert.Equal(2, (await Read<CrudPage<DictionaryDto>>(await staff.PostAsJsonAsync(U("/api/dic-authors/Search"), new CrudSearch(), Json))).Items[0].UseCount);
        Assert.Single(await Suggest(staff, "keyword", "phieu"));

        // Khung phân loại: thêm tay có diễn giải; trùng (không dấu/hoa thường) bị chặn.
        await Read<DictionaryDto>(await staff.PostAsJsonAsync(U("/api/dic-classes/Add"), new DictionaryRequest("895.9223", "Tiểu thuyết Việt Nam"), Json));
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(U("/api/dic-authors/Add"), new DictionaryRequest("to hoai"), Json)).StatusCode);
        var classes = await Suggest(staff, "class", "895.92");
        Assert.Equal(["895.922", "895.9223"], classes.Select(c => c.Value));
        Assert.Equal("Tiểu thuyết Việt Nam", classes[1].Description);

        // Quyền theo loại; đơn vị khác không thấy.
        var keywordsOnly = Staff(tenantId, "DIC_KEYWORDS:view");
        Assert.Equal(HttpStatusCode.Forbidden, (await keywordsOnly.PostAsJsonAsync(U("/api/dic-authors/Search"), new CrudSearch(), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await keywordsOnly.PostAsJsonAsync(U("/api/dic-keywords/Search"), new CrudSearch(), Json)).StatusCode);
        Assert.Empty(await Suggest(Staff(NewTenant()), "author", "to"));
    }

    [Fact]
    public async Task Isbd_config_per_bib_type_replaces_the_default_rules_on_opac()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var type = await Read<BibTypeDto>(await staff.PostAsJsonAsync(U("/api/bib-types/Add"), new BibTypeRequest("Sách", "S1"), Json));
        var bib = await Read<BibDto>(await staff.PostAsJsonAsync(U("/api/bibs/Add"), Book(type.Id, "Tắt đèn", "Ngô Tất Tố", "Văn học"), Json));
        var defaultIsbd = await Read<List<string>>(await staff.GetAsync(U($"/api/bibs/Isbd/{bib.PublicId}")));
        Assert.StartsWith("Tắt đèn / Ngô Tất Tố. — H. : Văn học, 2020", defaultIsbd[0], StringComparison.Ordinal);

        var template = await Read<List<IsbdRule>>(await staff.GetAsync(U("/api/isbd-configs/Template")));
        Assert.Contains(template, r => r.Tag == "245" && r.Code == "c" && r.Prefix == " / ");
        IsbdRule[] rules = [new() { Tag = "245" }, new() { Tag = "245", Code = "a", Prefix = "«", Suffix = "»" }, new() { Tag = "264", Prefix = " — " },
            new() { Tag = "264", Code = "b" }];
        var preview = await Read<List<string>>(await staff.PostAsJsonAsync(U("/api/isbd-configs/Preview"), new IsbdPreviewRequest(bib.Mfn, rules), Json));
        Assert.Equal(["«Tắt đèn /» — Văn học,"], preview);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(U("/api/isbd-configs/Add"),
            new IsbdConfigRequest(type.Id, [new IsbdRule { Tag = "24X" }]), Json)).StatusCode);
        await Read<IsbdConfigDto>(await staff.PostAsJsonAsync(U("/api/isbd-configs/Add"), new IsbdConfigRequest(type.Id, rules), Json));
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(U("/api/isbd-configs/Add"), new IsbdConfigRequest(type.Id, rules), Json)).StatusCode);

        Assert.Equal(["«Tắt đèn /» — Văn học,"], await Read<List<string>>(await staff.GetAsync(U($"/api/bibs/Isbd/{bib.PublicId}"))));
        var opac = factory.CreateClient();
        opac.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        opac.DefaultRequestHeaders.Add("X-Gw-Signature", Elib.BuildingBlocks.Tenancy.GatewaySignature.Compute("test-gateway-key-0123456789abcdef", tenantId));
        var marc = await Read<OpacMarcDto>(await opac.GetAsync(U($"/api/opac/bibs/{bib.PublicId}/marc")));
        Assert.Equal(["«Tắt đèn /» — Văn học,"], marc.Isbd);
    }

    [Fact]
    public async Task Bib_edit_history_records_marc_changes_per_tag_with_the_reason()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        var bib = await Read<BibDto>(await staff.PostAsJsonAsync(U("/api/bibs/Add"), Book(null, "Số đỏ", "Vũ Trọng Phụng", "Văn học"), Json));
        var edit = Book(null, "Số đỏ", "Vũ Trọng Phụng", "Hội Nhà văn", "Tiểu thuyết trào phúng");
        var request = new HttpRequestMessage(HttpMethod.Put, U($"/api/bibs/Update/{bib.PublicId}")) { Content = JsonContent.Create(edit, options: Json) };
        request.Headers.Add(CrudDiff.ReasonHeader, Uri.EscapeDataString("Sửa NXB theo bản in 2022"));
        await Read<BibDto>(await staff.SendAsync(request));

        var log = Assert.Single(factory.PublishedOf<AuditRecorded>(), a => a.TenantId == tenantId && a.EntityId == bib.PublicId.ToString() && a.Action == "UPDATE");
        var changes = JsonDocument.Parse(log.ChangesJson!).RootElement;
        Assert.Equal(("#1 $a H. : $b Văn học, $c 2020", "#1 $a H. : $b Hội Nhà văn, $c 2020"),
            (changes.GetProperty("264")[0].GetString(), changes.GetProperty("264")[1].GetString()));
        Assert.Equal(JsonValueKind.Null, changes.GetProperty("650")[0].ValueKind);
        Assert.Equal("Sửa NXB theo bản in 2022", changes.GetProperty("_reason").GetString());
        Assert.False(changes.TryGetProperty("245", out _));
        Assert.False(changes.TryGetProperty("005", out _));
        Assert.False(changes.TryGetProperty("fields", out _));
    }
}
