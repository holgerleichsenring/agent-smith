using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>p0393a: builds the DeriveSpec context from the pipeline state.</summary>
public sealed class DeriveSpecContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pipeline);
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is not null ? r : project.Repos;
        return new DeriveSpecContext(ticket, project.Tracker, repos, project.Agent, pipeline);
    }
}

/// <summary>Builds the spec-review context: the same scope the derivation ran over, plus
/// the agent whose model takes the review call.</summary>
public sealed class ReviewSpecContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pipeline);
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is not null ? r : project.Repos;
        return new ReviewSpecContext(ticket, repos, project.Agent, pipeline);
    }
}

/// <summary>p0393a: builds the hand-back context from the pipeline state.</summary>
public sealed class SpecHandbackContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pipeline);
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var repos = pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var r)
            && r is not null ? r : project.Repos;
        return new SpecHandbackContext(ticket, project.Tracker, repos, pipeline);
    }
}

/// <summary>p0393a: the sequence splice needs nothing but the run context.</summary>
public sealed class PhaseSequenceContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new PhaseSequenceContext(pipeline);
}

/// <summary>
/// p0393a: the phase id travels on the spliced COMMAND, not in the context — the
/// sequence stamps each block with its phase and this is where that stamp is read.
/// </summary>
public sealed class SelectPhaseContextBuilder : IContextBuilder
{
    public ICommandContext Build(
        PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new SelectPhaseContext(command.PhaseId ?? string.Empty, pipeline);
    }
}
