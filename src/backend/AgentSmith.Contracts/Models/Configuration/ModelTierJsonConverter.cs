using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>Writes a <see cref="ModelTier"/> as <c>strong</c>/<c>fast</c> and reads any casing.</summary>
public sealed class ModelTierJsonConverter : JsonStringEnumConverter<ModelTier>
{
    public ModelTierJsonConverter() : base(JsonNamingPolicy.CamelCase) { }
}
