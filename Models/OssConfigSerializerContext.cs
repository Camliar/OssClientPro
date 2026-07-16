using System.Text.Json.Serialization;

namespace OssClientPro.Models;

/// <summary>
/// Source-generated JSON serializer context for AOT-compatible serialization.
/// Used by ConfigService instead of the reflection-based JsonSerializer.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OssConfig))]
internal partial class OssConfigSerializerContext : JsonSerializerContext
{
}
