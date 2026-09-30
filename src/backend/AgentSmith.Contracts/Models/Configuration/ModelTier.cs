using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// The operator's word for what a catalog model is good for. The product never rates a
/// model; it only knows which roles decide structure and so need a strong one, and says so
/// when such a role resolves to an entry the operator marked fast. Stored and served as a
/// lowercase string (<c>strong</c> / <c>fast</c>) in JSON, and as <c>tier: strong</c> in YAML.
/// </summary>
[JsonConverter(typeof(ModelTierJsonConverter))]
public enum ModelTier
{
    Strong,
    Fast,
}
