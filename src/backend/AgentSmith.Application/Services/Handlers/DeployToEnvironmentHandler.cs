using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-18-3863c: the step of a 'deploy' run. It finds the binding and environment, reads the template's declaration
/// at its revision, decides the version by the template's rule, and hands the changed manifest to the GitOps delivery.
/// An environment already at the version is done with nothing written. Every refusal names what is missing.
/// </summary>
public sealed class DeployToEnvironmentHandler(DeploymentTemplateRead templates, DeployManifestPlan plans, GitOpsDelivery delivery)
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
        var (plan, planError) = await plans.PlanAsync(new DeployPlanInput(context, target, declaration, sha), cancellationToken);
        if (plan is null) return CommandResult.Fail(planError!);
        if (plan.Order is null) return CommandResult.Ok(plan.Headline);
        var (opened, sentence) = await delivery.DeliverAsync(plan.Order, cancellationToken);
        return opened ? CommandResult.Ok($"{plan.Headline}: {sentence}.") : CommandResult.Fail($"{plan.Headline} failed: {sentence}.");
    }
}
