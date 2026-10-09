namespace ELIBAPI.Core.Interfaces;

public interface IEmbeddingService
{
    /// <summary>Embed a document chunk (taskType: RETRIEVAL_DOCUMENT)</summary>
    Task<float[]> EmbedAsync(string text);

    /// <summary>Embed a user query (taskType: RETRIEVAL_QUERY)</summary>
    Task<float[]> EmbedQueryAsync(string text);
}
