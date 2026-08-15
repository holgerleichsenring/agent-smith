namespace AgentSmith.Contracts.Models.Workers;

/// <summary>
/// p0416: one model call, expressed as the payload an EXTERNAL worker answers instead
/// of a provider API. Carries everything the provider would have received — the full
/// message list (system prompt, history, tool results), the tool definitions with their
/// JSON schemas, and the run/step the call belongs to — because a worker that sees a
/// reduced picture is not exercising the run that ships.
/// <para>
/// This record IS the contract p0166e's inverted worker mode will carry: today it is
/// rendered into a CLI prompt, tomorrow it is the <c>params</c> of an MCP request the
/// worker client pulls. The transport changes; the payload does not.
/// </para>
/// </summary>
public sealed record WorkerRequest(
    string Protocol,
    string RequestId,
    string? RunId,
    int? StepIndex,
    string? Role,
    string? Phase,
    string? Repo,
    string AgentType,
    string Model,
    DateTimeOffset CreatedUtc,
    IReadOnlyList<WorkerMessage> Messages,
    IReadOnlyList<WorkerToolDefinition> Tools,
    WorkerRequestOptions Options)
{
    /// <summary>
    /// The one-line identity every failure names: which request, on which run, at which
    /// step. A timeout that cannot say what went unanswered is not a diagnosis.
    /// </summary>
    public string Describe() =>
        $"request {RequestId} (run={RunId ?? "-"} step={StepIndex?.ToString() ?? "-"} "
        + $"role={Role ?? "-"} phase={Phase ?? "-"})";
}
