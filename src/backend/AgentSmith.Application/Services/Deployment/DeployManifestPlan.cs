using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: plans one deploy — the declared environment, the version by the template's rule, the manifest as it
/// stands and the change to it, on a fresh branch named for the context, the environment and the run.
/// </summary>
public sealed partial class DeployManifestPlan(DeployVersion versions, DeploymentManifest manifests, ManifestVersionKey keys)
{
    /// <summary>The plan, or null with the reason.</summary>
    public async Task<(DeployPlan? Plan, string? Error)> PlanAsync(DeployPlanInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var (project, request, target) = (input.Context.Project, input.Context.Request!, input.Target);
        if (input.Declaration.Environment(request.Environment) is not { } declared)
            return (null, $"the deployment template declares no environment '{request.Environment}'");
        var (version, versionError) = await versions.DecideAsync(
            new DeployVersionQuestion(project, target, input.Declaration, declared, request.Version), cancellationToken);
        if (version is null) return (null, versionError);
        var (repo, repoError) = target.RepoOf(project, target.Environment);
        if (repo is null) return (null, repoError);
        var (text, current, readError) = await manifests.ReadAsync(repo, declared, cancellationToken);
        if (text is null) return (null, readError);
        var headline = $"Deploy of `{request.Context}` {version} to {declared.Name}";
        if (current == version)
            return (new DeployPlan($"{headline}: `{declared.Manifest}` of `{repo.Name}` already carries it; nothing written", null), null);
        var (changed, changeError) = keys.Replace(text, declared.VersionKey!, version);
        return changed is null ? (null, changeError) : (new DeployPlan(headline, Order(input, declared, repo, changed, version)), null);
    }

    private static GitOpsOrder Order(
        DeployPlanInput input, DeclaredEnvironment declared, RepoConnection repo, string content, string version)
    {
        var (request, project, binding) = (input.Context.Request!, input.Context.Project, input.Target.Binding);
        var run = input.Context.Pipeline.TryGet<string>(ContextKeys.RunId, out var id) && id is not null ? id : version;
        var branch = new BranchName($"agent-smith/deploy/{Segment(request.Context)}-{Segment(declared.Name)}/{Segment(run)}");
        var body = $"Delivers `{request.Context}` version `{version}` to `{declared.Name}`: sets `{declared.VersionKey}` in "
            + $"`{declared.Manifest}`. Per the deployment template `{binding.TemplateContext}` of `{binding.Repo.Name}` at "
            + $"`{input.TemplateSha ?? binding.Revision ?? "its default branch"}`, by agent-smith run {run}.";
        return new GitOpsOrder(repo, declared.Manifest!, content, branch, $"Deploy {request.Context} {version} to {declared.Name}",
            body, input.Target.Environment.Approval, project.Merge.Protection,
            new CiReadRequest(null, project.Merge.CiExcerptChars, project.Merge.CiFailedChecksRead), version);
    }

    private static string Segment(string value) => NotRefSafe().Replace(value, "-");

    [GeneratedRegex(@"[^A-Za-z0-9._-]")]
    private static partial Regex NotRefSafe();
}
