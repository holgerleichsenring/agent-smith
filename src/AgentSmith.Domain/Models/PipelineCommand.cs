namespace AgentSmith.Domain.Models;

/// <summary>
/// A typed pipeline command with optional parameters.
/// Replaces string-encoded commands like "SkillRoundCommand:architect:1".
/// </summary>
public sealed record PipelineCommand
{
    public string Name { get; }
    public string? SkillName { get; init; }
    public int? Round { get; init; }

    public PipelineCommand(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a parameterized skill round command.
    /// </summary>
    public static PipelineCommand SkillRound(string commandName, string skillName, int round) =>
        new(commandName) { SkillName = skillName, Round = round };

    /// <summary>
    /// Creates a simple command without parameters.
    /// </summary>
    public static PipelineCommand Simple(string commandName) => new(commandName);

    /// <summary>
    /// Parses a legacy string-encoded command for backward compatibility.
    /// "SkillRoundCommand:architect:1" → PipelineCommand with SkillName and Round.
    /// </summary>
    public static PipelineCommand Parse(string encoded)
    {
        var parts = encoded.Split(':');
        if (parts.Length >= 3 && int.TryParse(parts[2], out var round))
            return new PipelineCommand(parts[0]) { SkillName = parts[1], Round = round };
        if (parts.Length == 2)
            return new PipelineCommand(parts[0]) { SkillName = parts[1] };
        return new PipelineCommand(encoded);
    }

    public string DisplayName => SkillName is not null
        ? $"{Name}:{SkillName}{(Round.HasValue ? $":{Round}" : "")}"
        : Name;

    public override string ToString() => DisplayName;
}
