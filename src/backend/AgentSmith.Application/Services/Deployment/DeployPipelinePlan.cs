using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863e: plans a pipeline deploy — the declared environment, the version by the template's rule (as the
/// GitOps plan decides it), the repository the pipeline is defined in (the environment's repo, else the context's own),
/// and the inputs filled from the version, the environment, the context and the parameters.
/// </summary>
public sealed class DeployPipelinePlan(DeployVersion versions)
{
    /// <summary>The order, or null with the reason.</summary>
    public async Task<(PipelineTriggerOrder? Order, string? Error)> PlanAsync(DeployPlanInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (project, request, target) = (input.Context.Project, input.Context.Request!, input.Target);
        if (input.Declaration.Environment(request.Environment) is not { Pipeline: { } pipeline } declared)
            return (null, $"the deployment template declares no environment '{request.Environment}'");
        var (version, versionError) = await versions.DecideAsync(
            new DeployVersionQuestion(project, target, input.Declaration, declared, request.Version), cancellationToken);
        if (version is null) return (null, versionError);
        var (repo, repoError) = target.RepoOf(project, target.Environment);
        if (repo is null) return (null, repoError);
        var (inputs, inputError) = PipelineInputValues.Fill(pipeline.Inputs, Values(input, declared, version));
        if (inputs is null) return (null, $"environment '{declared.Name}': {inputError}");
        return (new PipelineTriggerOrder($"Deploy of `{request.Context}` {version} to {declared.Name}", repo,
            new PipelineRunOrder(pipeline.Name, pipeline.Ref, inputs),
            new CiReadRequest(null, project.Merge.CiExcerptChars, project.Merge.CiFailedChecksRead)), null);
    }

    private static Dictionary<string, string?> Values(DeployPlanInput input, DeclaredEnvironment declared, string version)
    {
        var values = new Dictionary<string, string?>(input.Declaration.Parameters ?? new Dictionary<string, string?>(), StringComparer.Ordinal);
        foreach (var (key, value) in input.Target.Environment.Parameters) values[key] = value;
        values[PipelineInputValues.Version] = version;
        values[PipelineInputValues.Environment] = declared.Name;
        values[PipelineInputValues.Context] = input.Context.Request!.Context;
        return values;
    }
}
