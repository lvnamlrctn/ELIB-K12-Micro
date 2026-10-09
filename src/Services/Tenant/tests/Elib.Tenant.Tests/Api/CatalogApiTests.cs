using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Tenancy;
using Elib.BuildingBlocks.Testing;
using Elib.Contracts.Events.Platform;
using Elib.Tenant.Application;

namespace Elib.Tenant.Tests.Api;

public sealed class CatalogApiTests : IClassFixture<TenantApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static long _nextUserId = 5000;

    private readonly TenantApiFactory _factory;

    public CatalogApiTests(TenantApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static Uri U(string path) => new(path, UriKind.Relative);

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString();

    private async Task<TenantDto> NewTenantAsync()
    {
        var code = "C" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var response = await _factory.CreateClient().WithClaims(TestClaims.SuperAdmin)
            .PostAsJsonAsync("/api/tenants", new CreateTenantRequest(code, "Trường " + code, code.ToLowerInvariant(), null, null, []), Json);
        return await Read<TenantDto>(response); // không chọn module nào và chỉ chờ identity — vẫn đủ để có danh mục
    }

    /// <summary>Nhân viên của đơn vị với tập quyền cho trước ("*" = toàn quyền).</summary>
    private HttpClient Staff(long tenantId, params string[] grants)
    {
        var userId = Interlocked.Increment(ref _nextUserId);
        _factory.Permissions.Grants[userId] = grants.Length == 0 ? ["*"] : grants;
        return _factory.CreateClient().WithClaims(TestClaims.Staff(tenantId, userId));
    }

    [Fact]
    public async Task New_tenant_gets_default_catalogs()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);

        var ethnicities = await Read<IReadOnlyList<NamedItemDto>>(await staff.PostAsJsonAsync("/api/ethnicities/SearchAll", new CrudSearch()));
        Assert.Equal(55, ethnicities.Count);
        var nationalities = await Read<CrudPage<NamedItemDto>>(await staff.PostAsJsonAsync("/api/nationalities/Search", new CrudSearch { Keyword = "Việt" }));
        Assert.Equal("Việt Nam", Assert.Single(nationalities.Items).Name);
        var currencies = await Read<IReadOnlyList<CurrencyDto>>(await staff.PostAsJsonAsync("/api/currencies/SearchAll", new CrudSearch()));
        Assert.Equal(["EUR", "USD", "VND"], currencies.Select(c => c.Code));

        var parameters = await Read<IReadOnlyList<SystemParameterDto>>(await staff.PostAsJsonAsync("/api/system-parameters/SearchAll", new SystemParameterSearch()));
        Assert.Contains(parameters, p => p.Code == "READER_AUTH_CONFIG" && p.IsBuiltIn && !p.IsPublic);

        // Chạy lại seed không thêm trùng.
        var again = await _factory.CreateClient().WithClaims(TestClaims.SuperAdmin).PostAsync(U($"/api/tenants/{tenant.PublicId}/defaults"), null);
        Assert.Equal(0, JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement.GetProperty("added").GetInt32());
    }

    [Fact]
    public async Task Crud_round_trip_and_duplicate_names()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);

        var created = await staff.PostAsJsonAsync("/api/academic-titles/Add", new NameRequest("  Phó giáo sư "));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var title = await Read<NamedItemDto>(created);
        Assert.Equal("Phó giáo sư", title.Name);

        Assert.Equal(title.PublicId, (await Read<NamedItemDto>(await staff.GetAsync(U($"/api/academic-titles/{title.Id}")))).PublicId);
        Assert.Equal(title.Id, (await Read<NamedItemDto>(await staff.GetAsync(U($"/api/academic-titles/GetById/{title.PublicId}")))).Id);

        var duplicate = await staff.PostAsJsonAsync("/api/academic-titles/Add", new NameRequest("Phó giáo sư"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("NAME_EXISTS", await ErrorCode(duplicate));

        var updated = await Read<NamedItemDto>(await staff.PutAsJsonAsync($"/api/academic-titles/Update/{title.PublicId}", new NameRequest("Giáo sư")));
        Assert.Equal("Giáo sư", updated.Name);
        Assert.NotNull(updated.UpdatedAt);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/academic-titles/Delete/{title.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U($"/api/academic-titles/GetById/{title.PublicId}"))).StatusCode);
        // Xoá mềm không chặn thêm lại cùng tên.
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync("/api/academic-titles/Add", new NameRequest("Giáo sư"))).StatusCode);

        var invalid = await staff.PostAsJsonAsync("/api/academic-titles/Add", new NameRequest(" "));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("NAME_INVALID", await ErrorCode(invalid));

        // Mỗi thao tác ghi thành công có một dòng nhật ký (AuditRecorded) cho service audit; thao tác lỗi thì không.
        var audit = _factory.PublishedOf<AuditRecorded>().Where(a => a.TenantId == tenant.Id && a.EntityType == "AcademicTitle").ToList();
        Assert.Equal(["ADD", "UPDATE", "DELETE", "ADD"], audit.Select(a => a.Action));
        Assert.Equal("Thêm học hàm học vị: Phó giáo sư", audit[0].Summary);
        Assert.Equal(title.PublicId.ToString(), audit[0].EntityId);
        Assert.All(audit, a => Assert.Equal("tenant", a.Service));
    }

    [Fact]
    public async Task Catalog_data_never_leaks_across_tenants()
    {
        var a = await NewTenantAsync();
        var b = await NewTenantAsync();
        var staffA = Staff(a.Id);
        var staffB = Staff(b.Id);

        var degree = await Read<NamedItemDto>(await staffA.PostAsJsonAsync("/api/degrees/Add", new NameRequest("Chỉ của A")));

        var searchB = await Read<CrudPage<NamedItemDto>>(await staffB.PostAsJsonAsync("/api/degrees/Search", new CrudSearch { Keyword = "chỉ của a" }));
        Assert.Empty(searchB.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await staffB.GetAsync(U($"/api/degrees/{degree.Id}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staffB.GetAsync(U($"/api/degrees/GetById/{degree.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staffB.PutAsJsonAsync($"/api/degrees/Update/{degree.PublicId}", new NameRequest("Chiếm"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staffB.DeleteAsync(U($"/api/degrees/Delete/{degree.PublicId}"))).StatusCode);

        // B dùng cùng tên được — tên chỉ duy nhất trong đơn vị.
        Assert.Equal(HttpStatusCode.Created, (await staffB.PostAsJsonAsync("/api/degrees/Add", new NameRequest("Chỉ của A"))).StatusCode);
        Assert.Equal("Chỉ của A", (await Read<NamedItemDto>(await staffA.GetAsync(U($"/api/degrees/GetById/{degree.PublicId}")))).Name);
    }

    [Fact]
    public async Task Permissions_follow_monolith_module_codes()
    {
        var tenant = await NewTenantAsync();
        var viewer = Staff(tenant.Id, "POSITIONS:view");

        Assert.Equal(HttpStatusCode.OK, (await viewer.PostAsJsonAsync("/api/positions/Search", new CrudSearch())).StatusCode);
        var add = await viewer.PostAsJsonAsync("/api/positions/Add", new NameRequest("Thủ thư trưởng"));
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/degrees/Search", new CrudSearch())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().PostAsJsonAsync("/api/positions/Search", new CrudSearch())).StatusCode);
    }

    [Fact]
    public async Task Currency_status_and_unique_code()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);

        var duplicate = await staff.PostAsJsonAsync("/api/currencies/Add", new CurrencyRequest("usd", "Đô", 1, null));
        Assert.Equal("CURRENCY_CODE_EXISTS", await ErrorCode(duplicate));

        var jpy = await Read<CurrencyDto>(await staff.PostAsJsonAsync("/api/currencies/Add", new CurrencyRequest("jpy", "Yên Nhật", 170, null)));
        Assert.Equal(("JPY", 2), (jpy.Code, jpy.Status));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PutAsJsonAsync("/api/currencies/ChangeStatus", new ChangeStatusRequest(jpy.PublicId, 1))).StatusCode);

        var inactive = await Read<IReadOnlyList<CurrencyDto>>(await staff.PostAsJsonAsync("/api/currencies/SearchAll", new CrudSearch { Status = 1 }));
        Assert.Equal("JPY", Assert.Single(inactive).Code);
        Assert.Equal("STATUS_INVALID", await ErrorCode(await staff.PutAsJsonAsync("/api/currencies/ChangeStatus", new ChangeStatusRequest(jpy.PublicId, 7))));

        // Danh mục không có trạng thái thì không có endpoint ChangeStatus.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await staff.PutAsJsonAsync("/api/degrees/ChangeStatus", new ChangeStatusRequest(jpy.PublicId, 1))).StatusCode);
    }

    [Fact]
    public async Task Org_tree_move_and_delete()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);

        async Task<OrgDto> Add(string name, long? parentId, int order = 0) =>
            await Read<OrgDto>(await staff.PostAsJsonAsync("/api/orgs/Add", new OrgRequest(name, parentId, order, null, null)));

        var school = await Add("Trường", null);
        var grade6 = await Add("Khối 6", school.Id, 1);
        var class6a = await Add("Lớp 6A", grade6.Id);
        var grade7 = await Add("Khối 7", school.Id, 2);
        Assert.Equal((1, 2, 3), (school.Level, grade6.Level, class6a.Level));

        var tree = await Read<IReadOnlyList<OrgTreeNode>>(await staff.PostAsJsonAsync("/api/orgs/GetTree", new OrgSearch()));
        var root = Assert.Single(tree);
        Assert.Equal(["Khối 6", "Khối 7"], root.Children.Select(c => c.Name));
        Assert.Equal("Trường", root.Children[0].ParentName);

        var filtered = await Read<IReadOnlyList<OrgTreeNode>>(await staff.PostAsJsonAsync("/api/orgs/GetTree", new OrgSearch { Keyword = "6a" }));
        Assert.Equal("Lớp 6A", Assert.Single(Assert.Single(Assert.Single(filtered).Children).Children).Name); // giữ đường về gốc

        var cycle = await staff.PutAsJsonAsync($"/api/orgs/Move/{grade6.PublicId}", new MoveOrgRequest(class6a.Id, 0));
        Assert.Equal("ORG_MOVE_CYCLE", await ErrorCode(cycle));

        var moved = await Read<OrgDto>(await staff.PutAsJsonAsync($"/api/orgs/Move/{grade6.PublicId}", new MoveOrgRequest(null, 5)));
        Assert.Equal((1, (long?)null), (moved.Level, moved.ParentId));
        Assert.Equal(2, (await Read<OrgDto>(await staff.GetAsync(U($"/api/orgs/GetById/{class6a.PublicId}")))).Level);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.PutAsJsonAsync("/api/orgs/UpdateOrder", new UpdateOrderRequest(grade7.PublicId, 9))).StatusCode);
        Assert.Equal(9, (await Read<OrgDto>(await staff.GetAsync(U($"/api/orgs/GetById/{grade7.PublicId}")))).SortOrder);

        var hasChildren = await staff.DeleteAsync(U($"/api/orgs/Delete/{grade6.PublicId}"));
        Assert.Equal("ORG_HAS_CHILDREN", await ErrorCode(hasChildren));
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/orgs/DeleteWithChildren/{grade6.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U($"/api/orgs/GetById/{class6a.PublicId}"))).StatusCode);

        var parentOfOther = await NewTenantAsync();
        var foreignParent = await Staff(parentOfOther.Id).PostAsJsonAsync("/api/orgs/Add", new OrgRequest("Lạ", school.Id, 0, null, null));
        Assert.Equal(HttpStatusCode.NotFound, foreignParent.StatusCode); // cha thuộc đơn vị khác = không tồn tại
    }

    [Fact]
    public async Task System_parameters_publish_changes_and_keep_secrets_private()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);

        var all = await Read<IReadOnlyList<SystemParameterDto>>(await staff.PostAsJsonAsync("/api/system-parameters/SearchAll", new SystemParameterSearch { Service = "identity" }));
        var secret = all.Single(p => p.Code == "READER_AUTH_CONFIG");

        // Đặt IsPublic = true cho tham số bí mật trong danh mục: bị bỏ qua.
        var updated = await Read<SystemParameterDto>(await staff.PutAsJsonAsync($"/api/system-parameters/Update/{secret.PublicId}",
            new SystemParameterRequest("READER_AUTH_CONFIG", "{\"Provider\":\"Ldap\",\"BindPassword\":\"bi-mat\"}", null, null, "json", "portal", IsPublic: true)));
        Assert.False(updated.IsPublic);
        Assert.Equal("identity", updated.Service);
        Assert.Contains(_factory.PublishedOf<SystemParameterChanged>(),
            e => e.TenantId == tenant.Id && e.Service == "identity" && e.Keys.SequenceEqual(["READER_AUTH_CONFIG"]));

        var renamed = await staff.PutAsJsonAsync($"/api/system-parameters/Update/{secret.PublicId}",
            new SystemParameterRequest("OTHER_CODE", "x", null, null, null, null));
        Assert.Equal("PARAMETER_CODE_IMMUTABLE", await ErrorCode(renamed));

        // Tham số riêng của đơn vị: tự chọn công khai được. Mã chuẩn hoá chữ hoa.
        var custom = await Read<SystemParameterDto>(await staff.PostAsJsonAsync("/api/system-parameters/Add",
            new SystemParameterRequest("Slogan", "Đọc sách mỗi ngày", "Khẩu hiệu", null, "text", null, IsPublic: true)));
        Assert.Equal(("SLOGAN", true, false), (custom.Code, custom.IsPublic, custom.IsBuiltIn));
        Assert.Equal("PARAMETER_CODE_EXISTS", await ErrorCode(await staff.PostAsJsonAsync("/api/system-parameters/Add",
            new SystemParameterRequest("slogan", "x", null, null, null, null))));

        await staff.PutAsJsonAsync($"/api/system-parameters/Update/{all.Single(p => p.Code == "READER_LOGIN_OTP_ENABLED").PublicId}",
            new SystemParameterRequest("READER_LOGIN_OTP_ENABLED", "1", null, null, "bool", null));

        // OPAC ẩn danh qua gateway.
        var opac = _factory.CreateClient();
        opac.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.Id.ToString(CultureInfo.InvariantCulture));
        opac.DefaultRequestHeaders.Add("X-Gw-Signature", GatewaySignature.Compute(TenantApiFactory.GatewayKey, tenant.Id));

        Assert.Equal("Đọc sách mỗi ngày", (await Read<PublicParameterDto>(await opac.GetAsync(U("/api/public/parameters/slogan")))).Value);
        Assert.Equal(HttpStatusCode.NotFound, (await opac.GetAsync(U("/api/public/parameters/READER_AUTH_CONFIG"))).StatusCode);
        var batch = await Read<IReadOnlyList<PublicParameterDto>>(await opac.GetAsync(U("/api/public/parameters?codes=LibraryName,READER_AUTH_CONFIG,READER_LOGIN_OTP_ENABLED,KHONG_CO")));
        Assert.Equal(["LIBRARYNAME", "READER_LOGIN_OTP_ENABLED"], batch.Select(p => p.Code));
        Assert.Equal("1", batch[1].Value);

        // Không có header gateway và không đăng nhập: không biết đơn vị nào.
        Assert.NotEqual(HttpStatusCode.OK, (await _factory.CreateClient().GetAsync(U("/api/public/parameters/slogan"))).StatusCode);

        // Service khác đọc giá trị hiệu lực (kể cả bí mật) qua /internal.
        var service = _factory.CreateClient().WithClaims(TestClaims.Service);
        var identityParams = await Read<Dictionary<string, string?>>(await service.GetAsync(U($"/internal/tenants/{tenant.Id}/parameters?service=identity")));
        Assert.Contains("bi-mat", identityParams["READER_AUTH_CONFIG"], StringComparison.Ordinal);
        Assert.Equal("1", identityParams["READER_LOGIN_OTP_ENABLED"]);
        Assert.DoesNotContain("SLOGAN", identityParams.Keys);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(U($"/internal/tenants/{tenant.Id}/parameters"))).StatusCode);
    }

    [Fact]
    public async Task Deleted_builtin_parameter_falls_back_to_catalog_default()
    {
        var tenant = await NewTenantAsync();
        var staff = Staff(tenant.Id);
        var all = await Read<IReadOnlyList<SystemParameterDto>>(await staff.PostAsJsonAsync("/api/system-parameters/SearchAll", new SystemParameterSearch { Keyword = "admin_login_otp" }));
        var otp = Assert.Single(all);

        await staff.PutAsJsonAsync($"/api/system-parameters/Update/{otp.PublicId}", new SystemParameterRequest(otp.Code, "1", null, null, null, null));
        await staff.DeleteAsync(U($"/api/system-parameters/Delete/{otp.PublicId}"));

        var service = _factory.CreateClient().WithClaims(TestClaims.Service);
        var values = await Read<Dictionary<string, string?>>(await service.GetAsync(U($"/internal/tenants/{tenant.Id}/parameters?service=identity")));
        Assert.Equal("0", values["ADMIN_LOGIN_OTP_ENABLED"]);
    }
}
