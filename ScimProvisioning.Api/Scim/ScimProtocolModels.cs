using System.Text.Json.Serialization;

namespace ScimProvisioning.Api.Scim;

/// <summary>SCIM 2.0 "ListResponse" envelope used by GET collection endpoints (RFC 7644 §3.4.2).</summary>
public class ScimListResponse<T>
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:ListResponse" };

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; } = 1;

    [JsonPropertyName("itemsPerPage")]
    public int ItemsPerPage { get; set; }

    [JsonPropertyName("Resources")]
    public List<T> Resources { get; set; } = new();
}

/// <summary>SCIM 2.0 error response (RFC 7644 §3.12).</summary>
public class ScimError
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:Error" };

    [JsonPropertyName("status")]
    public string Status { get; set; } = "400";

    [JsonPropertyName("scimType")]
    public string? ScimType { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;
}

/// <summary>SCIM 2.0 PATCH request body (RFC 7644 §3.5.2).</summary>
public class ScimPatchRequest
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:api:messages:2.0:PatchOp" };

    [JsonPropertyName("Operations")]
    public List<ScimPatchOperation> Operations { get; set; } = new();
}

public class ScimPatchOperation
{
    /// <summary>"add", "remove", or "replace".</summary>
    [JsonPropertyName("op")]
    public string Op { get; set; } = "replace";

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
