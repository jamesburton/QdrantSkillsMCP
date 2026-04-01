using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Qdrant.Client.Grpc;

namespace QdrantSkillsMCP.Infrastructure.Qdrant;

/// <summary>
/// <see cref="IQdrantOperations"/> implementation using the Qdrant REST API over HTTP.
/// Required for Azure App Service-hosted Qdrant which only exposes port 443 (no gRPC).
/// Translates between the gRPC-generated types used by consumers and JSON payloads.
/// </summary>
public sealed class RestQdrantOperations : IQdrantOperations
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RestQdrantOperations> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public RestQdrantOperations(HttpClient httpClient, ILogger<RestQdrantOperations> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // --- Collection operations ---

    public async Task<IReadOnlyList<string>> ListCollectionsAsync(CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync("/collections", cancellationToken);
        var collections = response?["result"]?["collections"]?.AsArray();
        if (collections is null) return [];

        return collections
            .Select(c => c?["name"]?.GetValue<string>() ?? "")
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList()
            .AsReadOnly();
    }

    public async Task CreateCollectionAsync(string collectionName, VectorParams vectorParams, CancellationToken cancellationToken)
    {
        var distanceName = vectorParams.Distance switch
        {
            Distance.Cosine => "Cosine",
            Distance.Euclid => "Euclid",
            Distance.Dot => "Dot",
            Distance.Manhattan => "Manhattan",
            _ => "Cosine"
        };

        var body = new JsonObject
        {
            ["vectors"] = new JsonObject
            {
                ["size"] = (long)vectorParams.Size,
                ["distance"] = distanceName
            }
        };

        await PutJsonAsync($"/collections/{Uri.EscapeDataString(collectionName)}", body, cancellationToken);
    }

    public async Task<CollectionInfo> GetCollectionInfoAsync(string collectionName, CancellationToken cancellationToken)
    {
        var response = await GetJsonAsync($"/collections/{Uri.EscapeDataString(collectionName)}", cancellationToken);
        var result = response?["result"]
            ?? throw new InvalidOperationException($"Failed to get collection info for '{collectionName}'");

        // Extract vector size from the response.
        // Qdrant REST returns vectors config as either:
        //   "params": { "size": N, "distance": "..." }  (named "")
        //   or nested under a name
        var vectorsConfig = result["config"]?["params"]?["vectors"];
        ulong size = 0;

        if (vectorsConfig is JsonObject obj)
        {
            // Could be { "size": N } directly or { "": { "size": N } }
            if (obj.ContainsKey("size"))
            {
                size = obj["size"]!.GetValue<ulong>();
            }
            else
            {
                // Take first named vector config
                var first = obj.FirstOrDefault();
                if (first.Value is JsonObject namedConfig && namedConfig.ContainsKey("size"))
                {
                    size = namedConfig["size"]!.GetValue<ulong>();
                }
            }
        }

        var distanceStr = vectorsConfig?["distance"]?.GetValue<string>()
            ?? vectorsConfig?.AsObject().FirstOrDefault().Value?["distance"]?.GetValue<string>()
            ?? "Cosine";

        var distance = distanceStr switch
        {
            "Euclid" => Distance.Euclid,
            "Dot" => Distance.Dot,
            "Manhattan" => Distance.Manhattan,
            _ => Distance.Cosine
        };

        // Build a CollectionInfo with just enough populated for consumers.
        var info = new CollectionInfo
        {
            Config = new CollectionConfig
            {
                Params = new CollectionParams
                {
                    VectorsConfig = new VectorsConfig
                    {
                        Params = new VectorParams
                        {
                            Size = size,
                            Distance = distance
                        }
                    }
                }
            }
        };

        return info;
    }

    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken)
    {
        var url = $"/collections/{Uri.EscapeDataString(collectionName)}";
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "DELETE", url, cancellationToken);
    }

    public async Task CreatePayloadIndexAsync(string collectionName, string fieldName, PayloadSchemaType schemaType, CancellationToken cancellationToken)
    {
        var typeName = schemaType switch
        {
            PayloadSchemaType.Keyword => "keyword",
            PayloadSchemaType.Integer => "integer",
            PayloadSchemaType.Float => "float",
            PayloadSchemaType.Bool => "bool",
            PayloadSchemaType.Geo => "geo",
            PayloadSchemaType.Text => "text",
            PayloadSchemaType.Datetime => "datetime",
            _ => "keyword"
        };

        var body = new JsonObject
        {
            ["field_name"] = fieldName,
            ["field_schema"] = typeName
        };

        var url = $"/collections/{Uri.EscapeDataString(collectionName)}/index";
        var response = await _httpClient.PutAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "PUT", url, cancellationToken);
    }

    // --- Point operations ---

    public async Task UpsertAsync(string collectionName, IReadOnlyList<PointStruct> points, CancellationToken cancellationToken)
    {
        var jsonPoints = new JsonArray();
        foreach (var p in points)
        {
            jsonPoints.Add(PointStructToJson(p));
        }

        var body = new JsonObject { ["points"] = jsonPoints };
        var url = $"/collections/{Uri.EscapeDataString(collectionName)}/points";
        var response = await _httpClient.PutAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "PUT", url, cancellationToken);
    }

    public async Task<IReadOnlyList<RetrievedPoint>> RetrieveAsync(string collectionName, Guid id, bool withPayload, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["ids"] = new JsonArray(PointIdToJson(id)),
            ["with_payload"] = withPayload,
            ["with_vector"] = false
        };

        var url = $"/collections/{Uri.EscapeDataString(collectionName)}/points";
        var response = await _httpClient.PostAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "POST", url, cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken);
        var resultArray = json?["result"]?.AsArray();
        if (resultArray is null) return [];

        return resultArray
            .Where(r => r is not null)
            .Select(r => JsonToRetrievedPoint(r!))
            .ToList()
            .AsReadOnly();
    }

    public async Task DeleteAsync(string collectionName, Guid id, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["points"] = new JsonArray(PointIdToJson(id))
        };

        var url = $"/collections/{Uri.EscapeDataString(collectionName)}/points/delete";
        var response = await _httpClient.PostAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "POST", url, cancellationToken);
    }

    public async Task SetPayloadAsync(string collectionName, IDictionary<string, Value> payload, Guid id, CancellationToken cancellationToken)
    {
        var jsonPayload = new JsonObject();
        foreach (var (key, value) in payload)
        {
            jsonPayload[key] = ValueToJsonNode(value);
        }

        var body = new JsonObject
        {
            ["payload"] = jsonPayload,
            ["points"] = new JsonArray(PointIdToJson(id))
        };

        var url = $"/collections/{Uri.EscapeDataString(collectionName)}/points/payload";
        var response = await _httpClient.PostAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "POST", url, cancellationToken);
    }

    public async Task<IReadOnlyList<ScoredPoint>> SearchAsync(
        string collectionName, float[] queryVector, Filter? filter, ulong limit, float? scoreThreshold, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["vector"] = JsonSerializer.SerializeToNode(queryVector),
            ["limit"] = (long)limit,
            ["with_payload"] = true
        };

        if (scoreThreshold.HasValue)
            body["score_threshold"] = scoreThreshold.Value;

        if (filter is not null)
            body["filter"] = FilterToJson(filter);

        var url = $"/collections/{Uri.EscapeDataString(collectionName)}/points/search";
        var response = await _httpClient.PostAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "POST", url, cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken);
        var resultArray = json?["result"]?.AsArray();
        if (resultArray is null) return [];

        return resultArray
            .Where(r => r is not null)
            .Select(r => JsonToScoredPoint(r!))
            .ToList()
            .AsReadOnly();
    }

    public async Task<ScrollResponse> ScrollAsync(string collectionName, Filter? filter = null, CancellationToken cancellationToken = default, uint limit = 250)
    {
        var body = new JsonObject
        {
            ["with_payload"] = true,
            ["limit"] = limit
        };

        if (filter is not null)
            body["filter"] = FilterToJson(filter);

        var url = $"/collections/{Uri.EscapeDataString(collectionName)}/points/scroll";
        var response = await _httpClient.PostAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "POST", url, cancellationToken);

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken);
        var resultNode = json?["result"];

        var scrollResponse = new ScrollResponse();
        var pointsArray = resultNode?["points"]?.AsArray();
        if (pointsArray is not null)
        {
            foreach (var p in pointsArray)
            {
                if (p is null) continue;
                scrollResponse.Result.Add(JsonToRetrievedPoint(p));
            }
        }

        return scrollResponse;
    }

    // --- HTTP helpers ---

    private async Task<JsonNode?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "GET", url, cancellationToken);
        return await response.Content.ReadFromJsonAsync<JsonNode>(JsonOptions, cancellationToken);
    }

    private async Task PutJsonAsync(string url, JsonNode body, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PutAsync(url, JsonContent.Create(body, options: JsonOptions), cancellationToken);
        await EnsureSuccessAsync(response, "PUT", url, cancellationToken);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string method, string url, CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Qdrant REST {Method} {Url} failed with {StatusCode}: {Body}",
                method, url, (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"Qdrant REST API error: {method} {url} returned {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }
    }

    // --- JSON <-> gRPC type conversion ---

    private static JsonNode PointIdToJson(Guid id)
    {
        return JsonValue.Create(id.ToString());
    }

    private static JsonObject PointStructToJson(PointStruct point)
    {
        var obj = new JsonObject();

        // ID
        obj["id"] = point.Id.PointIdOptionsCase switch
        {
            PointId.PointIdOptionsOneofCase.Uuid => point.Id.Uuid,
            PointId.PointIdOptionsOneofCase.Num => JsonValue.Create(point.Id.Num),
            _ => point.Id.Uuid
        };

        // Vectors - handle the common case of a single unnamed vector
        if (point.Vectors is not null && point.Vectors.VectorsOptionsCase == Vectors.VectorsOptionsOneofCase.Vector)
        {
#pragma warning disable CS0612 // Vector.Data is obsolete but still functional in this Qdrant.Client version
            obj["vector"] = JsonSerializer.SerializeToNode(point.Vectors.Vector.Data);
#pragma warning restore CS0612
        }

        // Payload
        if (point.Payload.Count > 0)
        {
            var payloadObj = new JsonObject();
            foreach (var (key, value) in point.Payload)
            {
                payloadObj[key] = ValueToJsonNode(value);
            }
            obj["payload"] = payloadObj;
        }

        return obj;
    }

    private static JsonNode? ValueToJsonNode(Value value)
    {
        return value.KindCase switch
        {
            Value.KindOneofCase.StringValue => JsonValue.Create(value.StringValue),
            Value.KindOneofCase.IntegerValue => JsonValue.Create(value.IntegerValue),
            Value.KindOneofCase.DoubleValue => JsonValue.Create(value.DoubleValue),
            Value.KindOneofCase.BoolValue => JsonValue.Create(value.BoolValue),
            Value.KindOneofCase.NullValue => null,
            Value.KindOneofCase.ListValue => new JsonArray(
                value.ListValue.Values.Select(v => ValueToJsonNode(v)).ToArray()),
            Value.KindOneofCase.StructValue => StructToJsonObject(value.StructValue),
            _ => null
        };
    }

    private static JsonObject StructToJsonObject(Struct s)
    {
        var obj = new JsonObject();
        foreach (var (key, val) in s.Fields)
        {
            obj[key] = ValueToJsonNode(val);
        }
        return obj;
    }

    private static Value JsonNodeToValue(JsonNode? node)
    {
        if (node is null)
            return new Value { NullValue = NullValue.NullValue };

        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<bool>(out var boolVal))
                return boolVal;
            if (jv.TryGetValue<long>(out var longVal))
                return longVal;
            if (jv.TryGetValue<double>(out var doubleVal))
                return doubleVal;
            if (jv.TryGetValue<string>(out var strVal))
                return strVal ?? "";
            // Fallback: treat as string
            return node.ToJsonString();
        }

        if (node is JsonArray arr)
        {
            var listValue = new ListValue();
            foreach (var item in arr)
            {
                listValue.Values.Add(JsonNodeToValue(item));
            }
            return new Value { ListValue = listValue };
        }

        if (node is JsonObject obj)
        {
            var structValue = new Struct();
            foreach (var (key, val) in obj)
            {
                structValue.Fields[key] = JsonNodeToValue(val);
            }
            return new Value { StructValue = structValue };
        }

        return new Value { NullValue = NullValue.NullValue };
    }

    private static RetrievedPoint JsonToRetrievedPoint(JsonNode node)
    {
        var point = new RetrievedPoint();

        // Parse ID
        var idNode = node["id"];
        if (idNode is not null)
        {
            var idStr = idNode.GetValue<string>();
            point.Id = new PointId { Uuid = idStr };
        }

        // Parse payload
        var payloadNode = node["payload"]?.AsObject();
        if (payloadNode is not null)
        {
            foreach (var (key, val) in payloadNode)
            {
                point.Payload[key] = JsonNodeToValue(val);
            }
        }

        return point;
    }

    private static ScoredPoint JsonToScoredPoint(JsonNode node)
    {
        var point = new ScoredPoint();

        // Parse ID
        var idNode = node["id"];
        if (idNode is not null)
        {
            var idStr = idNode.GetValue<string>();
            point.Id = new PointId { Uuid = idStr };
        }

        // Parse score
        if (node["score"] is not null)
            point.Score = node["score"]!.GetValue<float>();

        // Parse payload
        var payloadNode = node["payload"]?.AsObject();
        if (payloadNode is not null)
        {
            foreach (var (key, val) in payloadNode)
            {
                point.Payload[key] = JsonNodeToValue(val);
            }
        }

        return point;
    }

    private static JsonObject FilterToJson(Filter filter)
    {
        var obj = new JsonObject();

        if (filter.Must.Count > 0)
        {
            var mustArray = new JsonArray();
            foreach (var condition in filter.Must)
            {
                mustArray.Add(ConditionToJson(condition));
            }
            obj["must"] = mustArray;
        }

        if (filter.Should.Count > 0)
        {
            var shouldArray = new JsonArray();
            foreach (var condition in filter.Should)
            {
                shouldArray.Add(ConditionToJson(condition));
            }
            obj["should"] = shouldArray;
        }

        if (filter.MustNot.Count > 0)
        {
            var mustNotArray = new JsonArray();
            foreach (var condition in filter.MustNot)
            {
                mustNotArray.Add(ConditionToJson(condition));
            }
            obj["must_not"] = mustNotArray;
        }

        return obj;
    }

    private static JsonObject ConditionToJson(Condition condition)
    {
        return condition.ConditionOneOfCase switch
        {
            Condition.ConditionOneOfOneofCase.Field => FieldConditionToJson(condition.Field),
            Condition.ConditionOneOfOneofCase.Filter => new JsonObject { ["filter"] = FilterToJson(condition.Filter) },
            _ => new JsonObject()
        };
    }

    private static JsonObject FieldConditionToJson(FieldCondition field)
    {
        var matchObj = new JsonObject();

        if (field.Match is not null)
        {
            matchObj = field.Match.MatchValueCase switch
            {
                Match.MatchValueOneofCase.Boolean => new JsonObject { ["value"] = field.Match.Boolean },
                Match.MatchValueOneofCase.Keyword => new JsonObject { ["value"] = field.Match.Keyword },
                Match.MatchValueOneofCase.Integer => new JsonObject { ["value"] = field.Match.Integer },
                Match.MatchValueOneofCase.Text => new JsonObject { ["text"] = field.Match.Text },
                _ => new JsonObject()
            };
        }

        return new JsonObject
        {
            ["key"] = field.Key,
            ["match"] = matchObj
        };
    }
}
