using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ELIBAPI.Infrastructure.Services;

public class PageChunk
{
    public int    PageNumber { get; set; }
    public int    ChunkIndex { get; set; }
    public string Text       { get; set; } = string.Empty;
}

public interface IPdfExtractorService
{
    Task<List<PageChunk>> ExtractChunksAsync(Stream pdfStream,
                                              int chunkSize = 500,
                                              int overlap   = 50);
}

public class PdfExtractorService : IPdfExtractorService
{
    public Task<List<PageChunk>> ExtractChunksAsync(Stream pdfStream,
                                                     int chunkSize = 500,
                                                     int overlap   = 50)
    {
        var result = new List<PageChunk>();

        // Đọc stream vào buffer vì PdfPig cần seekable stream
        byte[] buffer;
        using (var ms = new MemoryStream())
        {
            pdfStream.CopyTo(ms);
            buffer = ms.ToArray();
        }

        using var doc = PdfDocument.Open(buffer);

        foreach (var page in doc.GetPages())
        {
            List<string> words;
            try
            {
                words = page.GetWords()
                            .Select(w => w.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .ToList();

                if (words.Count == 0)
                {
                    var raw = page.Text;
                    if (!string.IsNullOrWhiteSpace(raw))
                        words = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            catch (Exception)
            {
                // Bỏ qua trang có cấu trúc PDF lỗi (XObject thiếu, font lỗi...)
                continue;
            }

            if (words.Count == 0) continue;

            var chunks = SplitIntoChunks(words, chunkSize, overlap);
            for (int i = 0; i < chunks.Count; i++)
            {
                result.Add(new PageChunk
                {
                    PageNumber = page.Number,
                    ChunkIndex = i,
                    Text       = chunks[i]
                });
            }
        }

        return Task.FromResult(result);
    }

    private static List<string> SplitIntoChunks(List<string> words, int chunkSize, int overlap)
    {
        var chunks = new List<string>();

        if (words.Count <= chunkSize)
        {
            chunks.Add(string.Join(" ", words));
            return chunks;
        }

        int step = chunkSize - overlap;
        if (step <= 0) step = chunkSize;

        for (int start = 0; start < words.Count; start += step)
        {
            var slice = words.Skip(start).Take(chunkSize).ToList();
            if (slice.Count < 100 && chunks.Count > 0)
            {
                chunks[^1] = chunks[^1] + " " + string.Join(" ", slice);
                break;
            }
            chunks.Add(string.Join(" ", slice));
            if (start + chunkSize >= words.Count) break;
        }

        return chunks;
    }
}
