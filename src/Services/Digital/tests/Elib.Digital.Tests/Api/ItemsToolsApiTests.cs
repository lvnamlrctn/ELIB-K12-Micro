using System.Net;
using System.Net.Http.Json;
using Elib.Digital.Application;

namespace Elib.Digital.Tests.Api;

/// <summary>Tra ISBN, gợi ý phân loại bằng AI và khả năng tích hợp đang bật (như ELIB-LRC).</summary>
public sealed class ItemsToolsApiTests(DigitalApiFactory factory) : DigitalTestBase(factory)
{
    [Theory]
    [InlineData("978-0-7475-3269-9", true)]
    [InlineData("9780747532699", true)]
    [InlineData("0-7475-3269-9", true)]
    [InlineData("080442957X", true)]
    [InlineData("9780747532690", false)] // sai chữ số kiểm
    [InlineData("0747532690", false)]
    [InlineData("12345", false)]
    [InlineData("abcdefghijklm", false)]
    [InlineData("", false)]
    public void Isbn_format_checks_length_and_check_digit(string raw, bool valid) =>
        Assert.Equal(valid, IsbnFormat.TryNormalize(raw, out _));

    [Fact]
    public void An_isbn10_converts_to_isbn13() => Assert.Equal("9780747532699", IsbnFormat.ToIsbn13("0747532699"));

    [Fact]
    public async Task Isbn_lookup_returns_metadata_rejects_a_bad_isbn_and_reports_an_unknown_one()
    {
        var staff = Staff(NewTenant());
        Factory.Isbn.Books["9780747532699"] = new IsbnMetadata("9780747532699", "Harry Potter", null, ["J. K. Rowling"], "Bloomsbury", "1997", 223, "en", "Phù thủy nhỏ", ["Fantasy"], "Google Books");

        var found = await Read<IsbnMetadata>(await staff.GetAsync(U("/api/items/LookupIsbn?isbn=978-0-7475-3269-9")));
        Assert.Equal(("Harry Potter", "Bloomsbury", 223), (found.Title, found.Publisher, found.Pages));
        Assert.Equal(["J. K. Rowling"], found.Authors);

        Assert.Equal("ISBN_INVALID", (await Error(await staff.GetAsync(U("/api/items/LookupIsbn?isbn=123")))).Code);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync(U("/api/items/LookupIsbn?isbn=9786042000017"))).StatusCode);
    }

    [Fact]
    public async Task Tools_need_add_or_edit_permission()
    {
        var tenantId = NewTenant();
        Factory.Ai.IsEnabled = true;
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_ITEMS:view").GetAsync(U("/api/items/LookupIsbn?isbn=9780747532699"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Staff(tenantId, "DIGITAL_ITEMS:view").PostAsJsonAsync(U("/api/items/SuggestClassification"),
            new SuggestClassificationRequest("Sách"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Staff(tenantId, "DIGITAL_ITEMS:view").GetAsync(U("/api/items/Capabilities"))).StatusCode);
    }

    [Fact]
    public async Task Capabilities_tell_the_ui_which_integrations_are_switched_on()
    {
        var staff = Staff(NewTenant());
        Factory.Ai.IsEnabled = false;
        Factory.Embedder.IsEnabled = false;
        var off = await Read<ItemCapabilities>(await staff.GetAsync(U("/api/items/Capabilities")));
        Assert.Equal((true, false, true, false), (off.Isbn, off.Ai, off.Content, off.Embedding));

        Factory.Ai.IsEnabled = true;
        Factory.Embedder.IsEnabled = true;
        var on = await Read<ItemCapabilities>(await staff.GetAsync(U("/api/items/Capabilities")));
        Assert.Equal((true, true, true, true), (on.Isbn, on.Ai, on.Content, on.Embedding));
    }

    [Fact]
    public async Task Ai_suggestion_is_refused_when_off_and_offers_the_tenants_own_subjects_and_topics_when_on()
    {
        var staff = Staff(NewTenant());
        var subject = await Read<EbookSubjectDto>(await staff.PostAsJsonAsync(U("/api/subjects/Add"), new EbookSubjectRequest("Kinh tế"), Json));
        var topic = await Read<EbookTopicDto>(await staff.PostAsJsonAsync(U("/api/topics/Add"), new EbookTopicRequest("Tài chính"), Json));
        var body = new SuggestClassificationRequest("Kinh tế vi mô", "Giáo trình");

        Factory.Ai.IsEnabled = false;
        Assert.Equal("AI_DISABLED", (await Error(await staff.PostAsJsonAsync(U("/api/items/SuggestClassification"), body, Json))).Code);

        Factory.Ai.IsEnabled = true;
        Factory.Ai.Result = new ClassificationSuggestion(subject.Id, subject.Name, topic.Id, topic.Name, ["kinh tế", "vi mô"]);
        var suggestion = await Read<ClassificationSuggestion>(await staff.PostAsJsonAsync(U("/api/items/SuggestClassification"), body, Json));
        Assert.Equal((subject.Id, topic.Id), (suggestion.SubjectId, suggestion.TopicId));
        Assert.Equal(["kinh tế", "vi mô"], suggestion.Keywords);
        var call = Factory.Ai.LastCall!.Value;
        Assert.Equal("Kinh tế vi mô", call.Title);
        Assert.Contains(call.Subjects, o => o.Id == subject.Id);
        Assert.Contains(call.Topics, o => o.Id == topic.Id);

        Assert.Equal("TITLE_INVALID", (await Error(await staff.PostAsJsonAsync(U("/api/items/SuggestClassification"), new SuggestClassificationRequest("  "), Json))).Code);

        Factory.Ai.Result = null; // mô hình không trả được kết quả dùng được
        var unavailable = await staff.PostAsJsonAsync(U("/api/items/SuggestClassification"), body, Json);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Equal("AI_UNAVAILABLE", (await Error(unavailable)).Code);
    }
}
