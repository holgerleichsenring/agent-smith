namespace AgentSmith.Domain.ValueObjects;

/// <summary>
/// Name of a configured project.
/// </summary>
public sealed record ProjectName
{
    public string Value { get; }

    public ProjectName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public static implicit operator string(ProjectName name) => name.Value;
    public static implicit operator ProjectName(string value) => new(value);
    public override string ToString() => Value;
}
