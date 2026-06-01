namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Outcome of parsing a context.yaml file. One of three shapes:
/// <list type="bullet">
///   <item><see cref="Summary"/> set, <see cref="ErrorReason"/> null —
///     valid YAML, summary returned.</item>
///   <item>both null — content was empty / whitespace / structurally
///     unmatched (no meta block); not an error worth surfacing.</item>
///   <item><see cref="ErrorReason"/> set, <see cref="Summary"/> null —
///     YAML scanner / parser rejected the input. <see cref="ErrorReason"/>
///     carries the line/col + the original library message so operators
///     see WHY (e.g. "(Line: 22, Col: 7): found character '@' that
///     cannot start any token") instead of a downstream symptom.</item>
/// </list>
/// </summary>
public sealed record ContextYamlParseResult(
    ContextYamlSummary? Summary,
    string? ErrorReason)
{
    public static ContextYamlParseResult Ok(ContextYamlSummary summary) =>
        new(summary, null);
    public static ContextYamlParseResult Empty() =>
        new(null, null);
    public static ContextYamlParseResult Error(string reason) =>
        new(null, reason);
}
