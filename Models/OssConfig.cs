using System.Text.Json.Serialization;

namespace OssClientPro.Models;

/// <summary>
/// OSS client configuration model, persisted to config.json.
/// </summary>
public class OssConfig
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("accessKeyId")]
    public string AccessKeyId { get; set; } = string.Empty;

    [JsonPropertyName("accessKeySecret")]
    public string AccessKeySecret { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// UI language culture name (e.g. "zh-CN", "en-US").
    /// </summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Default bucket name. When set and valid, auto-selects this bucket after connect.
    /// Leave empty to manually pick from the bucket list.
    /// </summary>
    [JsonPropertyName("defaultBucket")]
    public string DefaultBucket { get; set; } = string.Empty;

    /// <summary>
    /// Optional file directory prefix within the bucket. When set, only objects under
    /// this path are displayed, and the prefix is stripped from display names.
    /// Leave empty to browse from the bucket root.
    /// </summary>
    [JsonPropertyName("basePrefix")]
    public string BasePrefix { get; set; } = string.Empty;

    /// <summary>
    /// Time display format: "auto" (follow OS), "12h", or "24h".
    /// Defaults to "auto".
    /// </summary>
    [JsonPropertyName("timeFormat")]
    public string TimeFormat { get; set; } = "auto";

    /// <summary>
    /// Returns true if all required credential fields are populated.
    /// </summary>
    [JsonIgnore]
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(AccessKeySecret);
}
