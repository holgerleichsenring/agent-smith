using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-18-3863c: the step of a 'deploy' run. It finds the binding and environment, reads the template's declaration
/// at its revision and hands it to the delivery the template declares (2026-11-18-3863e: a GitOps pull request or a
/// pipeline run), which decides the version by the template's rule. Every refusal names what is missing.
/// </summary>
public sealed class DeployToEnvironmentHandler(DeploymentTemplateRead templates, IEnumerable<IDeployDelivery> deliveries)
    : ICommandHandler<DeployToEnvironmentContext>
{
    public async Task<CommandResult> ExecuteAsync(DeployToEnvironmentContext context, CancellationToken cancellationToken)
    {
        if (context.Request is not { } request)
            return CommandResult.Fail("A 'deploy' run needs the context and environment the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.DeployContext)} and ContextKeys.{nameof(ContextKeys.DeployEnvironment)}. Nothing was delivered.");
        var (target, error) = DeploymentTarget.Of(context.Project, request);
        if (target is null) return CommandResult.Fail(error!);
        var (declaration, sha, readError) = await templates.ReadAsync(context.Project, target.Binding, cancellationToken);
        if (declaration is null) return CommandResult.Fail(readError!);
        var delivery = deliveries.Single(d => d.Delivery == declaration.Delivery); // the parser admits only these
        return await delivery.DeliverAsync(new DeployPlanInput(context, target, declaration, sha), cancellationToken);
    }
}
