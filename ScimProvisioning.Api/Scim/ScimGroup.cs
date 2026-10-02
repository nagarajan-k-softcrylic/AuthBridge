using System.Text.Json.Serialization;

namespace ScimProvisioning.Api.Scim;

/// <summary>SCIM 2.0 "Group" resource (RFC 7643 §4.2). AuthBridge roles (ApplicationAdmin,
/// Recruiter, ReportViewer, ...) are mapped to SCIM groups when synced to downstream apps.</summary>
public class ScimGroup
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new() { "urn:ietf:params:scim:schemas:core:2.0:Group" };

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ScimMember> Members { get; set; } = new();

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; set; }
}

public class ScimMember
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}
