using System.Net;
using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Contracts.Events.Platform;
using Elib.Serials.Application;
using Elib.Serials.Domain;

namespace Elib.Serials.Tests.Api;

public sealed class CatalogAndDomainTests(SerialsApiFactory factory) : SerialsTestBase(factory)
{
    [Fact]
    public async Task Types_frequencies_and_patterns_are_crud_catalogs_with_unique_names_and_protected_when_in_use()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);

        var type = await Read<NamedItemDto>(await Post(staff, "/api/types/Add", new NameRequest("Báo ngày")));
        Assert.Equal(HttpStatusCode.Conflict, (await Post(staff, "/api/types/Add", new NameRequest("Báo ngày"))).StatusCode);
        var types = await Read<CrudPage<NamedItemDto>>(await Post(staff, "/api/types/Search", new CrudSearch { Keyword = "báo" }));
        Assert.Contains(types.Items, t => t.PublicId == type.PublicId);

        var frequency = await AddFrequency(staff, "Hằng tuần", FrequencyUnit.Week);
        Assert.Equal(("Tuần", 1), (frequency.UnitName, frequency.IssuesPerUnit));
        Assert.Equal("NAME_EXISTS", await ErrorCode(await Post(staff, "/api/frequencies/Add", new FrequencyData("Hằng tuần", FrequencyUnit.Week, 1))));
        Assert.Equal("FREQUENCY_RATE_REQUIRED", await ErrorCode(await Post(staff, "/api/frequencies/Add", new FrequencyData("Thiếu số", FrequencyUnit.Day))));
        Assert.Equal("FREQUENCY_UNIT_INVALID", await ErrorCode(await Post(staff, "/api/frequencies/Add", new FrequencyData("Đơn vị lạ", 9, 1))));
        var edited = await Read<FrequencyDto>(await staff.PutAsJsonAsync(U($"/api/frequencies/Update/{frequency.PublicId}"), new FrequencyData("Mỗi tuần", FrequencyUnit.Week, 2), Json));
        Assert.Equal(("Mỗi tuần", 2), (edited.Name, edited.IssuesPerUnit));

        var pattern = await AddPattern(staff, "Số/năm");
        Assert.Equal(("Số {X}/{Y}", 12), (pattern.LabelTemplate, pattern.MaxX));
        Assert.Equal("STEP_INVALID", await ErrorCode(await Post(staff, "/api/patterns/Add", new PatternData("Bước 0", StepX: 0))));

        // Đăng ký đang dùng thì không xoá được tần suất / mẫu / loại.
        await AddSubscription(staff, new SubscriptionRequest("Báo X", PublicationTypeId: type.Id, FrequencyId: edited.Id, PatternId: pattern.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await staff.DeleteAsync(U($"/api/types/Delete/{type.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.DeleteAsync(U($"/api/frequencies/Delete/{edited.PublicId}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.DeleteAsync(U($"/api/patterns/Delete/{pattern.PublicId}"))).StatusCode);
        var spare = await AddPattern(staff, "Không dùng");
        Assert.Equal(HttpStatusCode.NoContent, (await staff.DeleteAsync(U($"/api/patterns/Delete/{spare.PublicId}"))).StatusCode);
    }

    [Fact]
    public void Numbering_resets_X_into_Y_and_Y_into_Z_and_labels_follow_the_template()
    {
        var pattern = NumberingPattern.Create(new PatternData("Bộ", null, "Bộ {Z} · Tập {Y} · Số {X}", MaxX: 3, MaxY: 2, StepX: 1));
        var n = pattern.Initial(null, null, null);
        Assert.Equal((1, 1, 1), (n.X, n.Y, n.Z));
        var seq = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            seq.Add(pattern.Label(n));
            n = pattern.Next(n);
        }
        Assert.Equal([
            "Bộ 1 · Tập 1 · Số 1", "Bộ 1 · Tập 1 · Số 2", "Bộ 1 · Tập 1 · Số 3", "Bộ 1 · Tập 2 · Số 1", "Bộ 1 · Tập 2 · Số 2", "Bộ 1 · Tập 2 · Số 3",
            "Bộ 2 · Tập 1 · Số 1", "Bộ 2 · Tập 1 · Số 2"], seq);

        var year = NumberingPattern.Create(new PatternData("Năm", null, "Số {X}/{Y}", MaxX: 12));
        var start = year.Initial(11, 2026, null);
        Assert.Equal("Số 12/2026", year.Label(year.Next(start)));
        Assert.Equal("Số 1/2027", year.Label(year.Next(year.Next(start))));
        Assert.Equal("Số 5", NumberingPattern.Create(new PatternData("Đơn")).Label(new IssueNumber(5, null, null)));
    }

    [Theory]
    [InlineData(FrequencyUnit.Day, 1, null, 3, "2026-01-18")]
    [InlineData(FrequencyUnit.Week, 1, null, 2, "2026-01-29")]
    [InlineData(FrequencyUnit.Week, null, 2, 2, "2026-02-12")] // hai tuần một kỳ
    [InlineData(FrequencyUnit.Month, 1, null, 1, "2026-02-15")]
    [InlineData(FrequencyUnit.Quarter, 1, null, 2, "2026-07-15")]
    [InlineData(FrequencyUnit.Year, 1, null, 2, "2028-01-15")]
    [InlineData(FrequencyUnit.Month, 2, null, 2, "2026-02-15")] // nửa tháng: 2 kỳ = 1 tháng
    [InlineData(FrequencyUnit.Month, 2, null, 1, "2026-01-30")] // nửa tháng: 1 kỳ ≈ 15,2 ngày
    public void Planned_dates_follow_the_frequency_unit_from_the_anchor(int unit, int? issuesPerUnit, int? unitsPerIssue, int step, string expected)
    {
        var f = Frequency.Create(new FrequencyData("Thử", unit, issuesPerUnit, unitsPerIssue));
        Assert.Equal(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), f.PlannedDate(new DateOnly(2026, 1, 15), step));
    }

    [Fact]
    public void Monthly_dates_come_from_the_anchor_times_the_step_so_month_end_does_not_drift()
    {
        var f = Frequency.Create(new FrequencyData("Tháng", FrequencyUnit.Month, 1));
        var anchor = new DateOnly(2026, 1, 31);
        Assert.Equal(new DateOnly(2026, 2, 28), f.PlannedDate(anchor, 1));
        Assert.Equal(new DateOnly(2026, 3, 31), f.PlannedDate(anchor, 2));
        Assert.Equal(new DateOnly(2026, 4, 30), f.PlannedDate(anchor, 3));
    }

    [Fact]
    public async Task New_tenant_gets_default_types_frequencies_and_patterns_and_a_bib_replica_and_the_seeded_signal()
    {
        var tenantId = NewTenant();
        var bib = new Elib.Contracts.Events.Catalog.BibChanged { TenantId = tenantId, BibPublicId = Guid.CreateVersion7(), Mfn = 9_000 + tenantId, Title = "Báo Nhân Dân", Status = 2 };
        Factory.CatalogBibs.Bibs[(tenantId, bib.Mfn)] = bib;
        await Factory.PublishAsync(new TenantProvisioned
        {
            TenantId = tenantId, Code = "S" + tenantId, Name = "Trường " + tenantId, Subdomain = "s" + tenantId, TimeZone = "Asia/Ho_Chi_Minh",
            Modules = [new ModuleLicense("SERIALS", "Active", null, null)],
        });
        await SerialsApiFactory.WaitForAsync(() => Task.FromResult(Factory.PublishedOf<TenantSeeded>().FirstOrDefault(s => s.TenantId == tenantId)), "TenantSeeded");

        var staff = Staff(tenantId);
        var types = await Read<IReadOnlyList<NamedItemDto>>(await Post(staff, "/api/types/SearchAll", new CrudSearch()));
        Assert.Equal(["Báo", "Tạp chí"], types.Select(t => t.Name).Order());
        var frequencies = await Read<IReadOnlyList<FrequencyDto>>(await Post(staff, "/api/frequencies/SearchAll", new CrudSearch()));
        Assert.Equal(FrequencyResource.Defaults.Count, frequencies.Count);
        var patterns = await Read<IReadOnlyList<PatternDto>>(await Post(staff, "/api/patterns/SearchAll", new CrudSearch()));
        Assert.Equal(PatternResource.Defaults.Count, patterns.Count);
        var found = await Read<IReadOnlyList<BibOption>>(await Post(staff, "/api/bibs/Lookup", new CrudSearch { Keyword = "nhan dan" }));
        Assert.Equal("Báo Nhân Dân", Assert.Single(found).Title);
    }

    [Fact]
    public async Task Buying_the_module_later_adds_the_default_catalogs_idempotently()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        Assert.Empty(await Read<IReadOnlyList<FrequencyDto>>(await Post(staff, "/api/frequencies/SearchAll", new CrudSearch())));

        var license = new Elib.Contracts.Events.Platform.ModuleLicenseChanged
        {
            TenantId = tenantId, SourceVersion = 1, Modules = [new ModuleLicense("SERIALS", "Active", null, null)],
        };
        await Factory.PublishAsync(license);
        var frequencies = await SerialsApiFactory.WaitForAsync(async () =>
        {
            var list = await Read<IReadOnlyList<FrequencyDto>>(await Post(staff, "/api/frequencies/SearchAll", new CrudSearch()));
            return list.Count == 0 ? null : list;
        }, "danh mục tần suất mặc định sau khi mua phân hệ");
        Assert.Equal(FrequencyResource.Defaults.Count, frequencies.Count);

        await Factory.PublishAsync(license with { SourceVersion = 2 }); // nhận lại: không nhân đôi
        await Task.Delay(500);
        Assert.Equal(FrequencyResource.Defaults.Count, (await Read<IReadOnlyList<FrequencyDto>>(await Post(staff, "/api/frequencies/SearchAll", new CrudSearch()))).Count);
    }

    [Fact]
    public async Task Access_needs_the_serials_license_and_the_right_permission_and_tenants_are_isolated()
    {
        var tenantId = NewTenant();
        var staff = Staff(tenantId);
        await AddFrequency(staff, "Riêng của A", FrequencyUnit.Month);

        var other = Staff(NewTenant());
        var listed = await Read<IReadOnlyList<FrequencyDto>>(await Post(other, "/api/frequencies/SearchAll", new CrudSearch()));
        Assert.DoesNotContain(listed, f => f.Name == "Riêng của A");

        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Staff(NewTenant(licensed: false)), "/api/frequencies/SearchAll", new CrudSearch())).StatusCode);
        var viewOnly = Staff(tenantId, "FREQUENCIES:view");
        Assert.Equal(HttpStatusCode.OK, (await Post(viewOnly, "/api/frequencies/SearchAll", new CrudSearch())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(viewOnly, "/api/frequencies/Add", new FrequencyData("Cấm", FrequencyUnit.Day, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(viewOnly, "/api/subscriptions/Search", new SubscriptionSearch())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(viewOnly, "/api/issues/Predict", new IssuePredictRequest(Guid.NewGuid()))).StatusCode);
    }
}
