namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>2026-11-18-3863a: raw YAML shape of one environment of a deployment binding.</summary>
public sealed class RawDeploymentEnvironment
{
    public string Name { get; set; } = string.Empty;

    /// <summary>A repo ref from the repos catalog; absent means the bound context's own repository.</summary>
    public string? Repo { get; set; }

    /// <summary>Absent reads as true: a person merges the delivery's pull request.</summary>
    public bool? Approval { get; set; }

    /// <summary>Values of the parameters the template declares.</summary>
    public Dictionary<string, string>? Parameters { get; set; }
}
