using System.Text.Json.Serialization;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Server.Services.Events;

public sealed partial record RunSnapshot
{
    /// <summary>2026-11-24-35ff: whether the server started this run itself (a series beat, a landing's follow-up, a sweep),
    /// read from the pipeline's declared origin, so the runs list can hide it behind a toggle. Serialized as
    /// <c>system</c> on every path that carries a snapshot.</summary>
    [JsonPropertyName("system")]
    public bool IsSystem => Pipeline is not null && PipelinePresets.IsSystem(Pipeline);
}
