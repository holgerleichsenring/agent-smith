namespace AgentSmith.Contracts.WorkSpecs;

/// <summary>
/// p0390: one YamlDotNet configuration shared by emit and consume, so a spec
/// this system writes is always a spec this system can read back — the round
/// trip is enforced by construction, as it is for context.yaml (p0193).
/// </summary>
public interface IWorkSpecSerializer
{
    /// <summary>Render spec.yaml, schema header included.</summary>
    string Serialize(WorkSpec spec);

    /// <summary>Parse spec.yaml. Returns null when the text carries no spec.</summary>
    WorkSpec? Parse(string yaml);
}
