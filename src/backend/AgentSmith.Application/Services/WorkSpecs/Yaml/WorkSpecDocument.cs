namespace AgentSmith.Application.Services.WorkSpecs.Yaml;

/// <summary>
/// p0390: the mutable YamlDotNet shape of spec.yaml. Immutable records are the
/// contract; this class exists only so YamlDotNet has settable properties to
/// bind to, exactly as ContextYamlSerializer's ReadShape does.
/// </summary>
public sealed class WorkSpecDocument
{
    public string? Key { get; set; }
    public string? Goal { get; set; }
    public List<string>? Requirements { get; set; }
    public List<WorkSpecConstraintEntry>? Constraints { get; set; }
    public List<string>? Done { get; set; }
    public bool DoneIsReadOnly { get; set; }
    public List<string>? Assumptions { get; set; }
    public List<WorkSpecRevisionEntry>? Revisions { get; set; }
    public string? HandbackCase { get; set; }
    public string? HandbackReason { get; set; }
}
