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
        var ticketId = pipeline.Get<TicketId>(ContextKeys.TicketId);
        return new FetchTicketContext(ticketId, project.Tracker, pipeline);
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

