using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Output;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Delivers the report the master wrote (every file it created outside the run record)
/// through the IOutputStrategy the operator chose. A run whose master wrote no report
/// fails here rather than delivering an empty "no findings".
/// </summary>
public sealed class DeliverOutputHandler(
    IServiceProvider serviceProvider,
    IOutputDirectoryResolver outputDirectories,
    ILogger<DeliverOutputHandler> logger) : ICommandHandler<DeliverOutputContext>
{
    private const string DocumentSeparator = "\n\n---\n\n";

    public async Task<CommandResult> ExecuteAsync(
        DeliverOutputContext context, CancellationToken cancellationToken)
    {
        var strategy = serviceProvider.GetKeyedService<IOutputStrategy>(context.OutputFormat);
        if (strategy is null)
            return CommandResult.Fail($"Unknown output format: '{context.OutputFormat}'");

        var report = MasterReport(context.Pipeline);
        if (report is null)
            return CommandResult.Fail(
                "No analysis report to deliver: the master wrote no document outside the run record");

        var outputContext = new OutputContext(
            context.ProjectName, null, [], report,
            outputDirectories.Resolve(context.OutputDir), context.Pipeline);
        await strategy.DeliverAsync(outputContext, cancellationToken);
        logger.LogInformation("Delivered the master's report via {Format}", context.OutputFormat);
        return CommandResult.Ok($"Delivered via {context.OutputFormat} strategy");
    }

    private static string? MasterReport(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges, out var changes)
            || changes is null)
            return null;

        var documents = changes
            .Where(c => !RunRecordPaths.IsRunRecordPath(c.Path.Value) && !string.IsNullOrWhiteSpace(c.Content))
            .Select(c => c.Content)
            .ToList();
        return documents.Count == 0 ? null : string.Join(DocumentSeparator, documents);
    }
}
