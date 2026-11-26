namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-26-462aa: a context.yaml's <c>state.done</c> read out. <see cref="Yaml"/> is the file without the block;
/// it is null when there was no block (<see cref="None"/>) or when the block could not be removed safely, and then
/// <see cref="Error"/> says why.
/// </summary>
public sealed record ContextYamlDoneExtraction(
    string? Yaml, IReadOnlyList<KeyValuePair<string, string>> Entries, string? Error)
{
    public static ContextYamlDoneExtraction None { get; } = new(null, [], null);

    public static ContextYamlDoneExtraction Failed(string error) => new(null, [], error);
}
