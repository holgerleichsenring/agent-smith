using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Sandbox.Agent.Models;

public static class WireFormat
{
    public static JsonSerializerOptions Json { get; } = BuildJsonOptions();

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
