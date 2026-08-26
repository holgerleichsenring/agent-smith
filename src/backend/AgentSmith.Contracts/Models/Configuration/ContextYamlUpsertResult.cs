namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-08-26-364f: outcome of merging a written document into the context.yaml
/// that is already on disk. Either <see cref="Yaml"/> carries the merged file, or
/// <see cref="ParseError"/> says why the existing file could not be read — and then
/// nothing is written, because overwriting an unparseable file turns a recoverable
/// edit into the data loss the merge exists to prevent.
/// </summary>
public sealed record ContextYamlUpsertResult(string? Yaml, string? ParseError)
{
    public static ContextYamlUpsertResult Ok(string yaml) => new(yaml, null);

    public static ContextYamlUpsertResult Error(string parseError) => new(null, parseError);
}
