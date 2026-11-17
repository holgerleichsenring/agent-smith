using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>2026-11-17-5701b: builder for the FileTickets step. The project's repositories are the run's own — the
/// ones the run's prologue resolved and CheckoutSource clones (overrides applied) — never the configuration's
/// entry read a second time, so the filed branch is written into the repository a run checks out.</summary>
public sealed class FileTicketsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline) =>
        new FileTicketsContext(pipeline, RunProject.Of(project, pipeline));
}
