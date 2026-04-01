using System.Security.Cryptography;
using System.Text;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

internal static class QdrantPointIdHelper
{
    /// <summary>Deterministically converts a string key to a Qdrant point ID via SHA-256.</summary>
    public static Guid FromString(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16));
    }
}
