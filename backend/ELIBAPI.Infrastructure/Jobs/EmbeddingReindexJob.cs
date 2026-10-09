using ELIBAPI.Core.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace ELIBAPI.Infrastructure.Jobs;

public class EmbeddingReindexJob(
    IElasticsearchService elastic,
    IEmbeddingService embedding,
    ILogger<EmbeddingReindexJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 7200)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        logger.LogInformation("EmbeddingReindexJob bắt đầu");

        string? scrollId = null;
        int total = 0, failed = 0;

        do
        {
            var (items, nextScrollId) = await elastic.ScrollChunksWithoutEmbeddingAsync(scrollId, batchSize: 20);
            if (items.Count == 0) break;

            var updates = new List<(string, float[])>();
            foreach (var (chunkId, content) in items)
            {
                try
                {
                    var vec = await embedding.EmbedAsync(content);
                    if (vec.Length > 0) updates.Add((chunkId, vec));
                    total++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Embedding thất bại cho chunk {Id}", chunkId);
                    failed++;
                }

                await Task.Delay(50); // tránh rate limit Gemini
            }

            if (updates.Count > 0)
                await elastic.BulkUpdateEmbeddingsAsync(updates);

            scrollId = nextScrollId;
            logger.LogInformation("Đã xử lý {Total} chunks (lỗi: {Failed})", total, failed);

            if (scrollId != null)
                await Task.Delay(200); // delay giữa các batch
        }
        while (scrollId != null);

        logger.LogInformation("EmbeddingReindexJob hoàn thành: {Total} chunks, {Failed} lỗi", total, failed);
    }
}
