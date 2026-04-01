namespace MessengerWebhook.Models;

/// <summary>
/// Represents a text embedding vector with similarity calculation
/// </summary>
public record Embedding(float[] Vector)
{
    public int Dimensions => Vector.Length;

    /// <summary>
    /// Calculate cosine similarity with another embedding
    /// Range: [-1, 1] where 1 = identical, 0 = orthogonal, -1 = opposite
    /// </summary>
    public double CosineSimilarity(Embedding other)
    {
        if (Dimensions != other.Dimensions)
        {
            throw new ArgumentException(
                $"Embeddings must have same dimensions. Expected {Dimensions}, got {other.Dimensions}");
        }

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < Dimensions; i++)
        {
            dotProduct += Vector[i] * other.Vector[i];
            normA += Vector[i] * Vector[i];
            normB += other.Vector[i] * other.Vector[i];
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
