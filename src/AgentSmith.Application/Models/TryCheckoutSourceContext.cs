using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for fail-soft source checkout used by api-scan. Unlike
/// CheckoutSourceContext, the handler never fails the pipeline — when
/// the source cannot be resolved, the run continues in passive mode.
/// </summary>
public sealed record TryCheckoutSourceContext(
    SourceConfig Source,
    BranchName? Branch,
    PipelineContext Pipeline) : ICommandContext;
