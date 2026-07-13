using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0327: JSON-stable form of one <see cref="PipelineCommand"/> in a run
/// checkpoint's remaining-work list. A dedicated DTO (rather than serializing
/// PipelineCommand directly) keeps the persisted shape independent of the
/// domain record's constructor/immutability details and round-trips the full
/// parameter set — the legacy "Name:skill:round" string encoding would drop
/// RepoName/ContextName/Workdir on spliced follow-ups.
/// </summary>
public sealed record CheckpointCommand(
    string Name,
    string? SkillName = null,
    int? Round = null,
    string? RepoName = null,
    string? ContextName = null,
    string? Workdir = null)
{
    public static CheckpointCommand From(PipelineCommand command) => new(
        command.Name, command.SkillName, command.Round,
        command.RepoName, command.ContextName, command.Workdir);

    public PipelineCommand ToPipelineCommand() => new(Name)
    {
        SkillName = SkillName,
        Round = Round,
        RepoName = RepoName,
        ContextName = ContextName,
        Workdir = Workdir,
    };
}
