namespace AgentSmith.Application.Services.WorkSpecs.Yaml;

/// <summary>
/// p0390: YamlDotNet binding shape for one revision header. The timestamp is a
/// round-trip ISO-8601 STRING, not a bound DateTimeOffset: YamlDotNet's default
/// scalar handling drops the offset and reads the value back as default(), which
/// would silently erase the revision history the whole artifact exists to keep.
/// </summary>
public sealed class WorkSpecRevisionEntry
{
    public int Number { get; set; }
    public string? Cause { get; set; }
    public string? At { get; set; }
}
