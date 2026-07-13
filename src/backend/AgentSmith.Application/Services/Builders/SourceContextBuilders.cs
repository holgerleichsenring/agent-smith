using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

public sealed class FetchTicketContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        // p0322a: ticketless runs (CLI-triggered init-project) build with a null
        // TicketId — the handler skips the fetch instead of the builder throwing.
        var ticketId = pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var id) ? id : null;
        return new FetchTicketContext(ticketId, project.Tracker, pipeline);
    }
}

// p0331: ScopeRepos — post-FetchTicket, pre-CheckoutSource. Ticket is optional
// (ticketless runs only build the inventory); AgentConfig feeds the classifier.
public sealed class ScopeReposContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        return new ScopeReposContext(ticket, pipeline.Resolved().Agent, pipeline);
    }
}

public sealed class CheckoutSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var branch = pipeline.TryGet<string>(ContextKeys.CheckoutBranch, out var b) && !string.IsNullOrWhiteSpace(b)
            ? new BranchName(b)
            : pipeline.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId)
                ? TicketBranchNamer.Compose(ticketId!)
                : null;

        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        return new CheckoutSourceContext(repos, branch, pipeline);
    }
}

public sealed class TryCheckoutSourceContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        var primary = repos[0];
        var branch = string.IsNullOrWhiteSpace(primary.DefaultBranch)
            ? null
            : new BranchName(primary.DefaultBranch);

        return new TryCheckoutSourceContext(repos, branch, pipeline);
    }
}

public sealed class LoadCodingPrinciplesContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var path = pipeline.Resolved().CodingPrinciplesPath ?? ProjectMetaPaths.CodingPrinciples;
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadCodingPrinciplesContext(path, repo, pipeline);
    }
}

public sealed class LoadContextContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repo = pipeline.Get<Repository>(ContextKeys.Repository);
        return new LoadContextContext(repo, pipeline);
    }
}

public sealed class SetupRegistryAuthContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new SetupRegistryAuthContext(pipeline);
}

