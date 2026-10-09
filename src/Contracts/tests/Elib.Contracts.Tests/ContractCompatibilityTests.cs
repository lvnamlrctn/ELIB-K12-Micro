using System.Reflection;
using System.Runtime.CompilerServices;
using Elib.Contracts.Events;

namespace Elib.Contracts.Tests;

/// <summary>
/// Kiểm tra tương thích contract (docs 09, GĐ0): bề mặt public của Elib.Contracts.Events được ghi trong
/// <c>contracts.snapshot.txt</c> (commit cùng code). Service cũ và mới chạy song song khi deploy lần lượt, nên:
/// <list type="bullet">
/// <item>xoá/đổi tên/đổi kiểu (kể cả nullable) một trường hoặc event = PHÁ VỠ → test đỏ. Đổi nghĩa thì tạo event V2.</item>
/// <item>thêm trường <c>required</c> vào event đã có = PHÁ VỠ (message của bên phát bản cũ không có trường đó).</item>
/// <item>thêm event/trường có mặc định = tương thích, nhưng phải ghi vào snapshot:
///   <c>ELIB_UPDATE_CONTRACTS=1 dotnet test src/Contracts/tests/Elib.Contracts.Tests</c> rồi commit file snapshot.</item>
/// </list>
/// </summary>
public sealed class ContractCompatibilityTests
{
    private static readonly NullabilityInfoContext Nullability = new();

    [Fact]
    public void Public_contract_surface_matches_the_committed_snapshot_without_breaking_changes()
    {
        var path = SnapshotPath();
        var current = Describe(typeof(IntegrationEvent).Assembly);
        var baseline = File.Exists(path) ? File.ReadAllLines(path).Where(l => l.Length > 0 && !l.StartsWith('#')).ToList() : [];

        var removed = baseline.Except(current).ToList();
        var added = current.Except(baseline).ToList();
        var knownTypes = baseline.Where(l => l.StartsWith("type ", StringComparison.Ordinal)).Select(l => l.Split(' ')[1]).ToHashSet();
        var requiredOnExisting = added.Where(l => l.StartsWith("  ", StringComparison.Ordinal) && l.Contains(" required", StringComparison.Ordinal)
                                                  && knownTypes.Contains(OwnerOf(current, l))).ToList();

        Assert.True(removed.Count == 0,
            "Thay đổi PHÁ VỠ contract (xoá/đổi tên/đổi kiểu) — giữ trường cũ, đổi nghĩa thì tạo event V2:\n" + string.Join("\n", removed));
        Assert.True(requiredOnExisting.Count == 0,
            "Thêm trường required vào event đã có là PHÁ VỠ — cho giá trị mặc định thay vì required:\n" + string.Join("\n", requiredOnExisting));

        if (added.Count == 0) return;
        if (Environment.GetEnvironmentVariable("ELIB_UPDATE_CONTRACTS") == "1")
        {
            File.WriteAllLines(path, Header.Concat(current));
            return;
        }
        Assert.Fail("Contract có phần mới (tương thích) chưa ghi vào snapshot. Chạy: ELIB_UPDATE_CONTRACTS=1 dotnet test " +
                    "src/Contracts/tests/Elib.Contracts.Tests rồi commit contracts.snapshot.txt:\n" + string.Join("\n", added));
    }

    private static readonly string[] Header =
    [
        "# Bề mặt public của Elib.Contracts.Events — sinh bởi ContractCompatibilityTests, KHÔNG sửa tay.",
        "# Dòng bị xoá/đổi trong diff của PR = thay đổi phá vỡ contract.",
    ];

    /// <summary>Mỗi kiểu public: một dòng "type", rồi từng property (tên, kiểu, nullable, required) hoặc giá trị enum.</summary>
    internal static List<string> Describe(Assembly assembly)
    {
        var lines = new List<string>();
        foreach (var type in assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            var kind = type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsAbstract ? "abstract" : "class";
            var baseType = type.BaseType is { } b && b != typeof(object) && b != typeof(Enum) && b != typeof(ValueType) ? $" : {Name(b)}" : "";
            lines.Add($"type {type.FullName} {kind}{baseType}");
            if (type.IsEnum)
            {
                foreach (var name in Enum.GetNames(type)) lines.Add($"  {name} = {Convert.ToInt64(Enum.Parse(type, name), System.Globalization.CultureInfo.InvariantCulture)}");
                continue;
            }
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(p => p.Name != "EqualityContract").OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var nullable = Nullability.Create(p).ReadState == NullabilityState.Nullable && !p.PropertyType.IsValueType ? "?" : "";
                var required = p.GetCustomAttribute<RequiredMemberAttribute>() is not null ? " required" : "";
                lines.Add($"  {p.Name} : {Name(p.PropertyType)}{nullable}{required}");
            }
        }
        return lines;
    }

    private static string OwnerOf(List<string> lines, string member)
    {
        var index = lines.IndexOf(member);
        for (var i = index; i >= 0; i--)
            if (lines[i].StartsWith("type ", StringComparison.Ordinal)) return lines[i].Split(' ')[1];
        return "";
    }

    private static string Name(Type type) => type.IsGenericType
        ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(Name))}>"
        : type.FullName ?? type.Name;

    private static string SnapshotPath([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "contracts.snapshot.txt"));
}
