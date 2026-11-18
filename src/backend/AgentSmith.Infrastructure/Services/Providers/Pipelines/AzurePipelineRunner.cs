using System.Globalization;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Source;
using Microsoft.TeamFoundation.Build.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Pipelines;

/// <summary>
/// 2026-11-18-3863e: Azure DevOps — a run of the pipeline named (folder\name for one in a folder) with the inputs as its
/// YAML template parameters, on a branch or tag (none: the pipeline's default branch). A run is a build, so it is read
/// as one and a failed one is told by <see cref="AzureReposBuildEvidence"/>, as a pull request's failed build is.
/// </summary>
public sealed class AzurePipelineRunner(
    Func<CancellationToken, Task<BuildHttpClient>> builds, string project, string organizationUrl) : IPipelineRunner
{
    public async Task<PipelineRunStart> StartAsync(PipelineRunOrder order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (string.IsNullOrWhiteSpace(order.Pipeline))
            throw new ArgumentException("Azure DevOps starts a pipeline by its name (folder\\name in a folder); the environment names none");
        var client = await builds(cancellationToken);
        var definition = await DefinitionAsync(client, order.Pipeline.Trim(), cancellationToken);
        var build = new Build
        {
            Definition = new DefinitionReference { Id = definition.Id },
            SourceBranch = PipelineRef.Full(order.Ref),
            TemplateParameters = new Dictionary<string, string>(order.Inputs),
        };
        var queued = await client.QueueBuildAsync(build, project, cancellationToken: cancellationToken);
        var id = queued.Id.ToString(CultureInfo.InvariantCulture);
        return new PipelineRunStart(id, Url(id));
    }

    public async Task<PipelineRunState> ReadAsync(string runId, CiReadRequest bounds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        var client = await builds(cancellationToken);
        var build = await client.GetBuildAsync(project, int.Parse(runId, CultureInfo.InvariantCulture), cancellationToken: cancellationToken);
        var state = build.Status != BuildStatus.Completed ? CiStates.Pending
            : build.Result is BuildResult.Succeeded or BuildResult.PartiallySucceeded ? CiStates.Green
            : CiStates.Red;
        if (state != CiStates.Red) return new PipelineRunState(state, Url(runId), []);
        var excerpt = bounds.FailuresRead > 0
            ? await AzureReposBuildEvidence.ExcerptAsync(client, build.Project.Id, build.Id, bounds, cancellationToken)
            : $"The run ended {build.Result}.";
        return new PipelineRunState(state, Url(runId), [new CiFailure(build.Definition?.Name ?? "pipeline", Url(runId), excerpt)]);
    }

    private async Task<BuildDefinitionReference> DefinitionAsync(BuildHttpClient client, string pipeline, CancellationToken ct)
    {
        var cut = pipeline.LastIndexOf('\\');
        var (path, name) = cut < 0 ? (null, pipeline) : ("\\" + pipeline[..cut].Trim('\\'), pipeline[(cut + 1)..]);
        var found = await client.GetDefinitionsAsync(project, name, null, null, null, null, null, null, null, path, null, null,
            null, null, null, null, null, ct);
        return found.Count switch
        {
            1 => found[0],
            0 => throw new InvalidOperationException($"project '{project}' has no pipeline named '{pipeline}'"),
            _ => throw new InvalidOperationException($"project '{project}' has {found.Count} pipelines named '{name}'; "
                + "name it folder\\name"),
        };
    }

    private string Url(string id) => $"{organizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(project)}/_build/results?buildId={id}";
}
