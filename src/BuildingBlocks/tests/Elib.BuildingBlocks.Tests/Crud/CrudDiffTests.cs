using System.Text.Json;
using Elib.BuildingBlocks.Crud;

namespace Elib.BuildingBlocks.Tests.Crud;

public sealed class CrudDiffTests
{
    private sealed record Dto(long Id, Guid PublicId, string Name, string? CitizenId, string? PasswordHash, long? ClassId, DateOnly? ExpireDate,
        string FullName, DateTimeOffset CreatedAt);

    private static Dto Sample() => new(1, Guid.NewGuid(), "An", "001", "h1", 6, new DateOnly(2027, 5, 31), "Lê An", DateTimeOffset.UnixEpoch);

    [Fact]
    public void Only_changed_non_sensitive_fields_are_recorded_as_old_and_new()
    {
        var before = Sample();
        var after = before with { Name = "Ân", CitizenId = "002", PasswordHash = "h2", ClassId = null, FullName = "Lê Ân", CreatedAt = DateTimeOffset.UtcNow };

        var diff = CrudDiff.Compute(before, after, ["fullName"])!;

        Assert.Equal(["name", "classId"], diff.Select(p => p.Key));
        Assert.Equal("""{"name":["An","Ân"],"classId":[6,null]}""", diff.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Assert.Null(CrudDiff.Compute(before, before));
    }

    [Fact]
    public void Reason_is_url_decoded_trimmed_cut_and_merged_into_the_json()
    {
        Assert.Equal("Lên lớp", CrudDiff.Reason(Uri.EscapeDataString("  Lên lớp ")));
        Assert.Equal("50% giảm", CrudDiff.Reason("50% giảm")); // không phải chuỗi mã hoá hợp lệ → giữ nguyên
        Assert.Null(CrudDiff.Reason("   "));
        Assert.Equal(CrudDiff.MaxReasonLength, CrudDiff.Reason(new string('a', 900))!.Length);

        var json = JsonDocument.Parse(CrudDiff.ToJson(null, "Sửa sai chính tả")!).RootElement;
        Assert.Equal("Sửa sai chính tả", json.GetProperty(CrudDiff.ReasonKey).GetString());
        Assert.Null(CrudDiff.ToJson(null, null));
    }

    [Fact]
    public void Oversized_diff_keeps_field_names_without_values()
    {
        var before = Sample();
        var diff = CrudDiff.Compute(before, before with { Name = new string('x', 9000) })!;

        var json = CrudDiff.ToJson(diff, "dài");
        Assert.NotNull(json);
        Assert.True(json.Length <= CrudDiff.MaxJsonLength);
        var root = JsonDocument.Parse(json).RootElement;
        Assert.Equal("…", root.GetProperty("name")[1].GetString());
        Assert.Equal("dài", root.GetProperty(CrudDiff.ReasonKey).GetString());
    }
}
