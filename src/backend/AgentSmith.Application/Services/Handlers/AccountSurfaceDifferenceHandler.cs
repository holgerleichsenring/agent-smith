using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Surface;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-08-30-c6ec: states which capability the served interface offers that no declared
/// first-party client was found to exercise.
/// <para>
/// No verification standard carries the operator's question — "values changeable through
/// the API although only the UI should ever set them" — because the intent lives in the
/// CLIENT, not in the server. The machine-readable form of it is a difference, and each
/// entry is an OBSERVATION paired with the requirement that would decide whether it
/// matters. Nothing here is raised as a finding.
/// </para>
/// <para>
/// A run missing an input says which one. The single exception is a consumer declaration
/// that resolves against nothing: that FAILS, because a difference computed over a subset
/// the operator did not choose reads as a clean bill.
/// </para>
/// </summary>
public sealed class AccountSurfaceDifferenceHandler(
    IServedSurfaceReader servedSurfaceReader,
    IClientSurfaceReader clientSurfaceReader,
    ISurfaceDifferenceCalculator calculator,
    IVerificationCatalogue catalogue,
    ILogger<AccountSurfaceDifferenceHandler> logger)
    : ICommandHandler<AccountSurfaceDifferenceContext>
{
    public async Task<CommandResult> ExecuteAsync(
        AccountSurfaceDifferenceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<SwaggerSpec>(ContextKeys.SwaggerSpec, out var spec) || spec is null)
            return NotComputed(pipeline, "this run holds no served description of an interface");

        var consumers = ConsumerDeclarations.Resolve(Repos(pipeline), spec.Title);
        if (consumers.Unresolvable is not null) return Unresolvable(consumers.Unresolvable, spec.Title);
        if (!consumers.AnyDeclared)
            return NotComputed(pipeline, "no repository declares that it consumes this interface");

        var checkouts = SurfaceCheckouts.For(pipeline, consumers.Repos);
        if (checkouts is null)
            return NotComputed(pipeline,
                $"the declared consumer checkout(s) [{string.Join(", ", consumers.Repos)}] are not available to this run");

        var served = servedSurfaceReader.Read(spec);
        var usage = await clientSurfaceReader.ReadAsync(
            Request(checkouts, consumers, served, context.Agent),
            PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
        return usage is null
            ? NotComputed(pipeline, "the reading of the client call sites produced no readable report")
            : Computed(pipeline, calculator.Compute(served, usage, catalogue.Version), served.Count);
    }

    private static IReadOnlyList<RepoConnection> Repos(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is not null
            ? repos
            : [];

    private static ClientSurfaceRequest Request(
        SurfaceCheckouts checkouts, ConsumerResolution consumers,
        IReadOnlyList<ServedOperation> served, AgentConfig agent) =>
        new(checkouts.Sandboxes, checkouts.DefaultKey, checkouts.KeyToRepo, checkouts.RepoPath,
            consumers.Repos, served, agent);

    private CommandResult Computed(
        PipelineContext pipeline, SurfaceDifferenceReport report, int operations)
    {
        pipeline.Set(ContextKeys.SurfaceDifference, report);
        pipeline.Set(ContextKeys.VerificationCatalogueVersion, report.CatalogueVersion);
        logger.LogInformation(
            "Surface difference: {Differences} observation(s) over {Operations} served operation(s), "
            + "bounded by {Read} file(s) read and {Undecided} not decided",
            report.Differences.Count, operations,
            report.Account.FilesRead.Count, report.Account.FilesNotDecided.Count);
        return CommandResult.Ok(
            $"Surface difference: {report.Differences.Count} observation(s) over {operations} operation(s)"
            + $" — {report.Account.FilesNotDecided.Count} client file(s) not decided");
    }

    private CommandResult NotComputed(PipelineContext pipeline, string reason)
    {
        pipeline.Set(ContextKeys.SurfaceDifference, SurfaceDifferenceReport.NotComputed(reason));
        logger.LogInformation("Surface difference not computed — {Reason}", reason);
        return CommandResult.Ok($"Surface difference not computed — {reason}");
    }

    private CommandResult Unresolvable(string declared, string served)
    {
        logger.LogError(
            "A repository declares it consumes '{Declared}', which is not the interface this run "
            + "holds a description of ('{Served}')", declared, served);
        return CommandResult.Fail(
            $"A repository declares it consumes '{declared}', which is not the interface this run "
            + $"holds a description of ('{served}'). A difference computed over a subset the "
            + "operator did not choose would read as a clean bill.");
    }
}
