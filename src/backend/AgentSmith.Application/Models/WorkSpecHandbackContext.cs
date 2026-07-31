using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>p0390: input for the hand-back step.</summary>
public sealed record WorkSpecHandbackContext(
    Ticket? Ticket,
    TrackerConnection? Tracker,
    IReadOnlyList<RepoConnection> Repos,
    PipelineContext Pipeline) : ICommandContext;
