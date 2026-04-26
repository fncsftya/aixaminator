using Microsoft.Extensions.VectorData;

namespace Shared.AI.Data;

/// <summary>
/// Base class for document chunks that store vector embeddings
/// </summary>
public abstract class DocumentChunkBase
{
    [VectorStoreRecordKey]
    public ulong ChunkId { get; set; }

    [VectorStoreRecordData]
    public string Content { get; set; }

    /// <summary>
    /// The embedding vector for this chunk
    /// </summary>
    public abstract ReadOnlyMemory<float>? Embedding { get; set; }
}

/// <summary>
/// Document chunk for OpenAI embeddings (1536 dimensions)
/// </summary>
public class OpenAIDocumentChunk : DocumentChunkBase
{
    [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction.CosineDistance, IndexKind.Hnsw)]
    public override ReadOnlyMemory<float>? Embedding { get; set; }
}

/// <summary>
/// Document chunk for Google AI embeddings (768 dimensions)
/// </summary>
public class GoogleDocumentChunk : DocumentChunkBase
{
    [VectorStoreRecordVector(Dimensions: 768, DistanceFunction.CosineDistance, IndexKind.Hnsw)]
    public override ReadOnlyMemory<float>? Embedding { get; set; }
}

/// <summary>
/// Factory to create the appropriate DocumentChunk instance based on provider
/// </summary>
public static class DocumentChunkFactory
{
    public static DocumentChunkBase Create(string providerName)
    {
        return providerName.ToLowerInvariant() switch
        {
            "google" => new GoogleDocumentChunk(),
            "openai" or _ => new OpenAIDocumentChunk() // Default to OpenAI
        };
    }
}