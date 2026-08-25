using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.Sandbox.Wire;

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
        // 2026-08-25-0d01: every wire enum tolerates a value this build cannot name. The
        // two KINDS fall back to an explicit Unknown, because what a message is FOR has no
        // safe substitute and the receiver must be able to say so. The two formatting
        // preferences fall back to their own defaults: an unrecognised output mode or sort
        // order is a nicety, and refusing the whole message over one would be theatre.
        options.Converters.Add(new TolerantEnumConverter<StepKind>(StepKind.Unknown));
        options.Converters.Add(new TolerantEnumConverter<StepEventKind>(StepEventKind.Unknown));
        options.Converters.Add(new TolerantEnumConverter<GrepOutputMode>(GrepOutputMode.Content));
        options.Converters.Add(new TolerantEnumConverter<DirectorySortBy>(DirectorySortBy.Name));
        return options;
    }
}
