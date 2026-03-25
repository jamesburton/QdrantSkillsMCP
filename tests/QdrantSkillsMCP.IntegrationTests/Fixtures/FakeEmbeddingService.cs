using System.Security.Cryptography;
using System.Text;
using QdrantSkillsMCP.Core.Interfaces;

namespace QdrantSkillsMCP.IntegrationTests.Fixtures;

/// <summary>
/// Deterministic embedding service for integration tests.
/// Generates vectors by hashing text input and distributing hash bytes across float dimensions.
/// Different inputs produce different vectors with measurable cosine similarity differences.
/// </summary>
public sealed class FakeEmbeddingService : IEmbeddingService
{
    private readonly int _dimensions;

    public FakeEmbeddingService(int dimensions = 64)
    {
        _dimensions = dimensions;
    }

    public int Dimensions => _dimensions;

    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        var vector = GenerateDeterministicVector(text, _dimensions);
        return Task.FromResult(vector);
    }

    /// <summary>
    /// Generates a deterministic, normalized float vector from text.
    /// Uses SHA-256 hash repeatedly to fill the required dimensions.
    /// </summary>
    private static float[] GenerateDeterministicVector(string text, int dimensions)
    {
        var vector = new float[dimensions];
        var textBytes = Encoding.UTF8.GetBytes(text);

        // Generate enough hash bytes to fill all dimensions (4 bytes per float)
        var hashInput = textBytes;
        var hashIndex = 0;
        byte[] currentHash = SHA256.HashData(hashInput);

        for (var i = 0; i < dimensions; i++)
        {
            if (hashIndex + 4 > currentHash.Length)
            {
                // Chain hash: hash the previous hash to get more bytes
                hashInput = currentHash;
                currentHash = SHA256.HashData(hashInput);
                hashIndex = 0;
            }

            // Convert 4 bytes to a float in range [-1, 1]
            var intValue = BitConverter.ToInt32(currentHash, hashIndex);
            vector[i] = intValue / (float)int.MaxValue;
            hashIndex += 4;
        }

        // Normalize to unit vector for cosine similarity
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (var i = 0; i < dimensions; i++)
                vector[i] /= magnitude;
        }

        return vector;
    }
}
