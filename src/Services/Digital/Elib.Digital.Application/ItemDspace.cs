using System.IO.Compression;
using System.Text;
using Elib.BuildingBlocks.Crud;
using Elib.BuildingBlocks.Domain;
using Elib.Digital.Domain;
using Microsoft.EntityFrameworkCore;

namespace Elib.Digital.Application;

/// <summary>
/// Nhập/xuất gói DSpace Simple Archive (LRC: ImportDSpace, ExportDSpaceSaf): mỗi thư mục tài liệu có <c>dublin_core.xml</c>, tùy chọn <c>contents</c> và các file.
/// Khác LRC: kho K12 chỉ nhận PDF cho file tài liệu số nên file khác PDF bị bỏ qua kèm cảnh báo; mọi giới hạn kích thước được kiểm trước khi đọc nội dung.
/// </summary>
public sealed partial class EbookItemResource
{
    public const int MaxDspaceEntries = 5000;
    public const int MaxDspaceItems = 200;
    public const long MaxDspaceUncompressedBytes = 2L * 1024 * 1024 * 1024;
    public const long MaxDspaceFileBytes = 200L * 1024 * 1024;
    public const int MaxDspaceExportFiles = 1000;
    public const long MaxDspaceContentsBytes = 1_000_000;

    private sealed record DspaceFolder(string Path, ZipArchiveEntry Metadata, IReadOnlyList<ZipArchiveEntry> Files, IReadOnlyList<string> Warnings);

    public async Task<ItemImportResult> ImportDspaceAsync(Stream zipStream, ItemImportDefaults defaults, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        ArgumentNullException.ThrowIfNull(defaults);
        ZipArchive zip;
        try
        {
            zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or NotSupportedException)
        {
            throw new BusinessRuleException("IMPORT_FILE_INVALID", "Tệp không phải ZIP hợp lệ.");
        }
        using (zip)
        {
            var folders = Inspect(zip);
            var rows = new List<(string Label, Func<EbookItemFields> Build, Func<EbookItem, CancellationToken, Task<IEnumerable<string>>>? After)>();
            foreach (var folder in folders)
            {
                var f = folder;
                rows.Add((f.Path, () => ParseFolder(f, defaults), (item, token) => AttachFilesAsync(f, item, token)));
            }
            return await ImportCoreAsync(rows, ct);
        }
    }

    /// <summary>Kiểm gói trước khi đọc nội dung: số mục, tổng dung lượng giải nén, tên mục (chặn đường dẫn thoát thư mục — zip-slip) và gom theo thư mục tài liệu.</summary>
    private static List<DspaceFolder> Inspect(ZipArchive zip)
    {
        var entries = zip.Entries;
        if (entries.Count > MaxDspaceEntries) throw new BusinessRuleException("IMPORT_ZIP_TOO_MANY_ENTRIES", $"Gói có quá {MaxDspaceEntries} mục.");
        long total = 0;
        foreach (var e in entries)
        {
            if (!SafeEntryName(e.FullName))
                throw new BusinessRuleException("IMPORT_ZIP_UNSAFE_PATH", $"Gói có đường dẫn không hợp lệ: {Shorten(e.FullName)}.");
            total += e.Length;
            if (total > MaxDspaceUncompressedBytes)
                throw new BusinessRuleException("IMPORT_ZIP_TOO_LARGE", $"Gói giải nén vượt {MaxDspaceUncompressedBytes / 1024 / 1024 / 1024} GB.");
        }

        var result = new List<DspaceFolder>();
        var metadata = entries.Where(e => e.Name.Equals("dublin_core.xml", StringComparison.OrdinalIgnoreCase) && e.FullName.Contains('/', StringComparison.Ordinal))
            .OrderBy(e => e.FullName, StringComparer.Ordinal).ToList();
        if (metadata.Count == 0) throw new BusinessRuleException("IMPORT_FILE_EMPTY", "Gói không có thư mục tài liệu nào chứa dublin_core.xml.");
        if (metadata.Count > MaxDspaceItems) throw new BusinessRuleException("IMPORT_TOO_MANY_ROWS", $"Mỗi lần nhập tối đa {MaxDspaceItems} tài liệu.");
        foreach (var meta in metadata)
        {
            var folder = meta.FullName[..meta.FullName.LastIndexOf('/')];
            var prefix = folder + "/";
            // Chỉ file nằm trực tiếp trong thư mục tài liệu (không lấy file của thư mục lồng bên trong).
            var inFolder = entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && e.Length > 0 && !e.FullName[prefix.Length..].Contains('/', StringComparison.Ordinal)).ToList();
            var warnings = new List<string>();
            var contents = inFolder.FirstOrDefault(e => e.Name.Equals("contents", StringComparison.OrdinalIgnoreCase));
            IEnumerable<string> names;
            if (contents is not null && contents.Length > MaxDspaceContentsBytes)
            {
                warnings.Add($"{folder}: bỏ qua danh sách contents vì quá lớn.");
                contents = null;
            }
            if (contents is not null)
            {
                using var reader = new StreamReader(contents.Open(), Encoding.UTF8);
                names = reader.ReadToEnd().Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('\t')[0].Trim()).Where(l => l.Length > 0).ToList();
            }
            else
            {
                names = inFolder.Select(e => e.Name).Where(n => !n.Equals("contents", StringComparison.OrdinalIgnoreCase) && !n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            }

            var files = new List<ZipArchiveEntry>();
            foreach (var name in names)
            {
                var entry = inFolder.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (entry is null) warnings.Add($"{folder}: thiếu file {Shorten(name)} trong gói.");
                else if (!entry.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) warnings.Add($"{folder}/{Shorten(entry.Name)}: bỏ qua — kho chỉ nhận file PDF.");
                else if (entry.Length > MaxDspaceFileBytes) warnings.Add($"{folder}/{Shorten(entry.Name)}: bỏ qua — vượt {MaxDspaceFileBytes / 1024 / 1024} MB.");
                else files.Add(entry);
            }
            result.Add(new DspaceFolder(folder, meta, files, warnings));
        }
        return result;
    }

    private static bool SafeEntryName(string name) =>
        name.Length is > 0 and <= 400 && !name.StartsWith('/') && !name.StartsWith('\\') && !name.Contains('\\', StringComparison.Ordinal) && !name.Contains(':', StringComparison.Ordinal)
        && !name.Split('/').Any(segment => segment == "..");

    private static string Shorten(string value) => value.Length <= 80 ? value : value[..80] + "…";

    private static EbookItemFields ParseFolder(DspaceFolder folder, ItemImportDefaults defaults)
    {
        if (folder.Metadata.Length > MaxImportXmlBytes) throw new BusinessRuleException("IMPORT_FILE_SIZE", $"dublin_core.xml lớn hơn {MaxImportXmlBytes / 1000} KB.");
        using var xml = folder.Metadata.Open();
        return ItemDublinCore.Parse(xml, defaults);
    }

    /// <summary>Tải các PDF của thư mục lên kho rồi gắn vào tài liệu vừa tạo; file lỗi chỉ cảnh báo (tài liệu vẫn được nhập — như LRC).</summary>
    private async Task<IEnumerable<string>> AttachFilesAsync(DspaceFolder folder, EbookItem item, CancellationToken ct)
    {
        var warnings = new List<string>(folder.Warnings);
        var order = 0;
        foreach (var entry in folder.Files)
        {
            try
            {
                await using var content = entry.Open();
                var mediaId = await store.UploadAsync(Path.GetFileName(entry.Name), entry.Length, content, ct);
                Db.Set<EbookFile>().Add(EbookFile.Create(item.Id, mediaId, new EbookFileFields(Type: "Document", FileType: "application/pdf", FileSize: entry.Length, FileExt: "pdf", SortOrder: ++order)));
                await Db.SaveChangesAsync(ct);
            }
            catch (BusinessRuleException e)
            {
                warnings.Add($"{folder.Path}/{Shorten(entry.Name)}: không tải được file ({e.Message})");
            }
        }
        if (order > 0)
        {
            item.Touch(clock.GetUtcNow());
            await states.PublishAsync(item, false, ct, hasFile: true);
            await Db.SaveChangesAsync(ct);
        }
        return warnings;
    }

    /// <summary>
    /// Xuất gói DSpace kèm file PDF: ZIP dựng ra tệp tạm (tự xóa khi đóng luồng) — nội dung từng file đi qua luồng từ media vào ZIP, không nạp cả file vào bộ nhớ.
    /// Giới hạn: <see cref="MaxDspaceItems"/> tài liệu, <see cref="MaxDspaceExportFiles"/> file, <see cref="MaxDspaceUncompressedBytes"/> byte (theo dung lượng khai báo).
    /// File không đọc được ghi vào <c>export-errors.txt</c> trong gói thay vì làm hỏng cả lượt xuất.
    /// </summary>
    public async Task<Stream> ExportDspaceWithFilesAsync(ItemExportRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rows = await LoadForExportAsync(request.Selection, ct);
        if (rows.Count > MaxDspaceItems)
            throw new BusinessRuleException("EXPORT_TOO_MANY_ROWS", $"Xuất kèm file tối đa {MaxDspaceItems} tài liệu một lần — chọn ít hơn.");
        var ids = rows.Select(r => r.Item.Id).ToList();
        // File của tài liệu dùng chung nằm ở kho đơn vị chủ — gói xuất của đơn vị chỉ mang metadata cho các tài liệu đó.
        var files = (await Db.Set<EbookFile>().AsNoTracking().Where(f => ids.Contains(f.EbookItemId) && f.SourcePublicId == null).Where(PdfFiles.Predicate)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id).ToListAsync(ct)).GroupBy(f => f.EbookItemId).ToDictionary(g => g.Key, g => g.ToList());
        var fileCount = files.Values.Sum(v => v.Count);
        if (fileCount > MaxDspaceExportFiles) throw new BusinessRuleException("EXPORT_TOO_MANY_FILES", $"Có {fileCount} file — vượt giới hạn {MaxDspaceExportFiles} file một lần xuất.");
        var declared = files.Values.SelectMany(v => v).Sum(f => (long)(f.FileSize ?? 0));
        if (declared > MaxDspaceUncompressedBytes)
            throw new BusinessRuleException("EXPORT_TOO_LARGE", $"Tổng dung lượng file vượt {MaxDspaceUncompressedBytes / 1024 / 1024 / 1024} GB — chọn ít tài liệu hơn.");

        var temp = new FileStream(Path.Combine(Path.GetTempPath(), "elib-dspace-" + Guid.NewGuid().ToString("N") + ".zip"), FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            var errors = new List<string>();
            using (var zip = new ZipArchive(temp, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var item = rows[i].Item;
                    var folder = $"item_{i + 1:D3}";
                    await WriteTextAsync(zip, $"{folder}/dublin_core.xml", ItemDublinCore.Build(item), ct);
                    var contents = new StringBuilder();
                    var n = 0;
                    foreach (var file in files.GetValueOrDefault(item.Id) ?? [])
                    {
                        var name = n == 0 ? "document.pdf" : $"document_{n + 1}.pdf";
                        try
                        {
                            await using var source = await store.OpenReadAsync(file.MediaFileId, ct);
                            var entry = zip.CreateEntry($"{folder}/{name}", CompressionLevel.Fastest);
                            await using (var target = entry.Open()) await source.CopyToAsync(target, ct);
                            contents.Append(name).Append("\tbundle:ORIGINAL\n");
                            n++;
                        }
                        catch (Exception e) when (e is BusinessRuleException or NotFoundException or HttpRequestException)
                        {
                            errors.Add($"{item.Title}: không đọc được file #{file.Id} ({e.Message})");
                        }
                    }
                    if (contents.Length > 0) await WriteTextAsync(zip, $"{folder}/contents", contents.ToString(), ct);
                }
                if (errors.Count > 0) await WriteTextAsync(zip, "export-errors.txt", string.Join('\n', errors), ct);
            }
            temp.Position = 0;
            return temp;
        }
        catch
        {
            await temp.DisposeAsync();
            throw;
        }
    }

    private static async Task WriteTextAsync(ZipArchive zip, string path, string text, CancellationToken ct)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        await using var writer = entry.Open();
        await writer.WriteAsync(Encoding.UTF8.GetBytes(text), ct);
    }
}
