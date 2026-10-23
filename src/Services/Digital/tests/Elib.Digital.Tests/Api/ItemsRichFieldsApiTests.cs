using System.Net.Http.Json;
using Elib.BuildingBlocks.Crud;
using Elib.Digital.Application;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elib.Digital.Tests.Api;

/// <summary>Trường giàu của tài liệu số (tác giả họ/tên, người hướng dẫn, số định danh, tóm tắt…) và bộ lọc nâng cao như ELIB-LRC.</summary>
public sealed class ItemsRichFieldsApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    private static async Task<EbookItemDto> Add(HttpClient staff, EbookItemFields fields) =>
        await Read<EbookItemDto>(await staff.PostAsJsonAsync(U("/api/items/Add"), fields, Json));

    private static async Task<CrudPage<EbookItemDto>> Search(HttpClient staff, EbookItemSearch search) =>
        await Read<CrudPage<EbookItemDto>>(await staff.PostAsJsonAsync(U("/api/items/Search"), search, Json));

    [Fact]
    public async Task Rich_fields_round_trip_and_the_flat_strings_follow_the_lists()
    {
        var staff = Staff(NewTenant());
        var item = await Add(staff, new EbookItemFields(
            "Giáo trình cơ sở dữ liệu", Abstract: "Tóm tắt", Description: "Mô tả", JournalName: "Tạp chí CNTT", PublishDay: 5, PublishMonth: 11, PublishDate: "2019",
            Authors: [new EbookPerson { Last = "Nguyễn", First = "Văn A" }, new EbookPerson { Last = "Trần", First = "Thị B" }],
            Advisors: [new EbookPerson { Last = "Lê", First = "C", Title = "PGS.TS" }],
            Identifiers: [new EbookIdentifier { Type = "ISBN", Value = "978-604-1" }, new EbookIdentifier { Type = "issn", Value = "  " }]));

        Assert.Equal("Nguyễn Văn A; Trần Thị B", item.Author);
        Assert.Equal("Lê C", item.OldAuthor);
        Assert.Equal(2, item.Authors.Count);
        Assert.Equal("PGS.TS", Assert.Single(item.Advisors).Title);
        var identifier = Assert.Single(item.Identifiers); // dòng rỗng bị bỏ, loại chuẩn hoá chữ thường
        Assert.Equal(("isbn", "978-604-1"), (identifier.Type, identifier.Value));
        Assert.Equal((5, 11, 2019, "Tóm tắt", "Mô tả", "Tạp chí CNTT"), (item.PublishDay, item.PublishMonth, item.PublishYear, item.Abstract, item.Description, item.JournalName));

        var again = await Read<EbookItemDto>(await staff.GetAsync(U($"/api/items/GetById/{item.PublicId}")));
        Assert.Equal(item.Authors.Select(a => a.First), again.Authors.Select(a => a.First)); // đọc lại từ DB

        Assert.Equal("IDENTIFIER_TYPE_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/items/Add"),
            new EbookItemFields("X", Identifiers: [new EbookIdentifier { Type = "xyz", Value = "1" }]), Json))).Code);
        Assert.Equal("PUBLISH_MONTH_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/items/Add"), new EbookItemFields("X", PublishMonth: 13), Json))).Code);
    }

    [Fact]
    public async Task Legacy_writers_that_send_only_the_flat_string_still_get_a_consistent_list_and_updates_without_lists_keep_the_strings()
    {
        var staff = Staff(NewTenant());
        var item = await Add(staff, new EbookItemFields("Số đỏ", Author: "Vũ Trọng Phụng; Nam Cao", OldAuthor: "Người HD", PublishDate: "05/2010"));
        Assert.Equal(["Vũ Trọng Phụng", "Nam Cao"], item.Authors.Select(a => a.Last)); // cả tên vào Last, không đoán họ/tên
        Assert.Equal("Người HD", Assert.Single(item.Advisors).Last);
        Assert.Equal(2010, item.PublishYear); // suy từ chuỗi ngày

        // Sửa không gửi danh sách (người gọi cũ): chuỗi mới thắng và danh sách dựng lại từ chuỗi.
        var updated = await Read<EbookItemDto>(await staff.PutAsJsonAsync(U($"/api/items/Update/{item.PublicId}"),
            new EbookItemFields("Số đỏ", Author: "Tác giả mới", PublishDate: "2012-01-01"), Json));
        Assert.Equal("Tác giả mới", updated.Author);
        Assert.Equal("Tác giả mới", Assert.Single(updated.Authors).Last);
        Assert.Equal(2012, updated.PublishYear);

        // Gửi danh sách rỗng tường minh = xoá hết.
        var cleared = await Read<EbookItemDto>(await staff.PutAsJsonAsync(U($"/api/items/Update/{item.PublicId}"), new EbookItemFields("Số đỏ", Authors: []), Json));
        Assert.Empty(cleared.Authors);
        Assert.Null(cleared.Author);
    }

    [Fact]
    public async Task Advanced_filters_match_author_publisher_title_year_range_free_and_submitted_days_in_vietnam_time()
    {
        var staff = Staff(NewTenant());
        await Add(staff, new EbookItemFields("Lập trình Python", Authors: [new EbookPerson { Last = "Hoàng", First = "Minh" }], Publisher: "NXB Giáo dục", PublishDate: "2015", Free: true));
        await Add(staff, new EbookItemFields("Cơ sở dữ liệu", Authors: [new EbookPerson { Last = "Phạm", First = "Dũng" }], Publisher: "NXB Bách khoa", PublishDate: "05/2021"));
        await Add(staff, new EbookItemFields("Không rõ năm", Publisher: "NXB Bách khoa"));

        Assert.Equal(["Lập trình Python"], (await Search(staff, new EbookItemSearch { Author = "hoàng" })).Items.Select(i => i.Title));
        Assert.Equal(2, (await Search(staff, new EbookItemSearch { Publisher = "bách khoa" })).TotalCount);
        Assert.Equal(["Cơ sở dữ liệu"], (await Search(staff, new EbookItemSearch { Title = "cơ sở" })).Items.Select(i => i.Title));
        Assert.Equal(["Cơ sở dữ liệu"], (await Search(staff, new EbookItemSearch { PublishYearFrom = 2020, PublishYearTo = 2022 })).Items.Select(i => i.Title));
        Assert.Equal(["Lập trình Python"], (await Search(staff, new EbookItemSearch { PublishYearTo = 2015 })).Items.Select(i => i.Title)); // tài liệu không có năm không lọt
        Assert.Equal(["Lập trình Python"], (await Search(staff, new EbookItemSearch { Free = true })).Items.Select(i => i.Title));

        // Ngày nộp: ngày lịch Việt Nam, gồm cả ngày "đến". Hôm nay (giờ VN) phải khớp cả 3; hôm qua chỉ làm "đến" thì rỗng; ngày mai làm "từ" thì rỗng.
        var today = DateOnly.FromDateTime(Factory.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);
        Assert.Equal(3, (await Search(staff, new EbookItemSearch { SubmittedFrom = today, SubmittedTo = today })).TotalCount);
        Assert.Equal(0, (await Search(staff, new EbookItemSearch { SubmittedTo = today.AddDays(-1) })).TotalCount);
        Assert.Equal(0, (await Search(staff, new EbookItemSearch { SubmittedFrom = today.AddDays(1) })).TotalCount);
    }

    [Fact]
    public async Task People_are_stored_as_json_with_pascal_case_keys_which_the_migration_backfill_also_writes()
    {
        var staff = Staff(NewTenant());
        var item = await Add(staff, new EbookItemFields("Kiểm tra khoá JSON", Authors: [new EbookPerson { Last = "Nguyễn", First = "An" }]));

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Elib.Digital.Infrastructure.DigitalDbContext>();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT authors FROM ebook_items WHERE public_id = $id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$id";
        parameter.Value = item.PublicId.ToString().ToUpperInvariant();
        command.Parameters.Add(parameter);
        if (command.Connection!.State != System.Data.ConnectionState.Open) await command.Connection.OpenAsync();
        var json = (string?)await command.ExecuteScalarAsync();
        // Migration EbookRichFields backfill ghi "Last" (jsonb_build_object('Last', …)) — hai bên phải trùng khoá, nếu không dữ liệu cũ đọc ra rỗng.
        Assert.Contains("\"Last\":", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_list_shows_the_file_count_per_item()
    {
        var staff = Staff(NewTenant());
        var item = await Add(staff, new EbookItemFields("Có hai file"));
        for (var i = 0; i < 2; i++)
            await staff.PostAsJsonAsync(U("/api/files/Add"), new EbookFileRequest(item.PublicId, Guid.CreateVersion7(), FileExt: "pdf", SortOrder: i), Json);
        Assert.Equal(2, Assert.Single((await Search(staff, new EbookItemSearch())).Items).TotalFile);
    }
}
