using Google.Protobuf.Collections;
using Qdrant.Client.Grpc;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

internal static class QdrantPayloadExtensions
{
    public static string GetString(this MapField<string, Value> payload, string key, string @default = "")
        => payload.TryGetValue(key, out var v) ? v.StringValue : @default;

    public static bool GetBool(this MapField<string, Value> payload, string key, bool @default = false)
        => payload.TryGetValue(key, out var v) ? v.BoolValue : @default;

    public static DateTimeOffset GetDateTimeOffset(this MapField<string, Value> payload, string key)
        => payload.TryGetValue(key, out var v)
            && DateTimeOffset.TryParse(v.StringValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt : DateTimeOffset.MinValue;

    public static string[]? GetStringList(this MapField<string, Value> payload, string key)
        => payload.TryGetValue(key, out var v)
            ? v.ListValue?.Values.Select(x => x.StringValue).ToArray()
            : null;
}
