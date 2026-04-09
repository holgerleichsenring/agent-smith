using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Central entry point: takes a PipelineRequest, loads config,
/// resolves the pipeline, and executes it end-to-end.
/// Also supports legacy string-based input for backward compatibility.
/// </summary>
public sealed class ExecutePipelineUseCase(
    IConfigurationLoader configLoader,
    IIntentParser intentParser,
    IPipelineExecutor pipelineExecutor,
    ILogger<ExecutePipelineUseCase> logger)
{
    public async Task<CommandResult> ExecuteAsync(
        PipelineRequest request, string configPath, CancellationToken cancellationToken)
    {
        logger.LogInformation("Executing pipeline '{Pipeline}' for project '{Project}'",
            request.PipelineName, request.ProjectName);

        var config = configLoader.LoadConfig(configPath);

        var projectName = request.ProjectName.ToLowerInvariant();
        if (!config.Projects.TryGetValue(projectName, out var projectConfig))
            throw new ConfigurationException($"Project '{projectName}' not found in configuration.");

        var commands = PipelinePresets.TryResolve(request.PipelineName)
            ?? throw new ConfigurationException($"Pipeline '{request.PipelineName}' not found in presets.");

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Headless, request.Headless);
        pipeline.Set(ContextKeys.PipelineTypeName, PipelinePresets.GetPipelineType(request.PipelineName));

        if (request.TicketId is not null)
            pipeline.Set(ContextKeys.TicketId, request.TicketId);

        if (request.IsInit)
        {
            pipeline.Set(ContextKeys.InitMode, true);
            pipeline.Set(ContextKeys.CheckoutBranch, "agentsmith/init");
        }

        if (request.Context is not null)
        {
            foreach (var (key, value) in request.Context)
                pipeline.Set(key, value);

            // Map ScanBranch to CheckoutBranch if not already set
            if (request.Context.ContainsKey(ContextKeys.ScanBranch)
                && !pipeline.Has(ContextKeys.CheckoutBranch))
            {
                pipeline.Set(ContextKeys.CheckoutBranch, request.Context[ContextKeys.ScanBranch]);
            }
        }

        var result = await pipelineExecutor.ExecuteAsync(
            commands, projectConfig, pipeline, cancellationToken);

        if (result.IsSuccess && pipeline.TryGet<string>(ContextKeys.PullRequestUrl, out var prUrl))
            result = result with { PrUrl = prUrl };

        LogResult(result, projectName);
        return result;
    }

    /// <summary>
    /// Legacy entry point for backward compatibility with string-based input.
    /// Parses intent from free text, builds a PipelineRequest, and delegates.
    /// </summary>
    public async Task<CommandResult> ExecuteAsync(
        string userInput,
        string configPath,
        bool headless,
        string? pipelineOverride,
        CancellationToken cancellationToken,
        Dictionary<string, object>? initialContext = null)
    {
        logger.LogInformation("Processing input: {Input}", userInput);

        var request = await BuildRequestFromLegacyInput(
            userInput, configPath, headless, pipelineOverride, initialContext, cancellationToken);

        return await ExecuteAsync(request, configPath, cancellationToken);
    }

    private async Task<PipelineRequest> BuildRequestFromLegacyInput(
        string userInput, string configPath, bool headless,
        string? pipelineOverride, Dictionary<string, object>? initialContext,
        CancellationToken cancellationToken)
    {
        var initMatch = Regex.Match(userInput, @"^init\s+(?:in\s+)?(\S+)$", RegexOptions.IgnoreCase);
        if (initMatch.Success)
        {
            return new PipelineRequest(
                initMatch.Groups[1].Value,
                pipelineOverride ?? "init-project",
                IsInit: true,
                Headless: headless,
                Context: initialContext);
        }

        var ticketlessMatch = Regex.Match(userInput,
            @"^(?:security-scan|legal-analysis|api-scan)\s+(?:in\s+)?(\S+)$", RegexOptions.IgnoreCase);
        if (ticketlessMatch.Success)
        {
            var config = configLoader.LoadConfig(configPath);
            var projectName = ticketlessMatch.Groups[1].Value.ToLowerInvariant();
            var pipeline = pipelineOverride;
            if (string.IsNullOrWhiteSpace(pipeline) && config.Projects.TryGetValue(projectName, out var pc))
                pipeline = pc.Pipeline;

            return new PipelineRequest(
                projectName,
                pipeline ?? "security-scan",
                Headless: headless,
                Context: initialContext);
        }

        var intent = await intentParser.ParseAsync(userInput, cancellationToken);
        var config2 = configLoader.LoadConfig(configPath);
        var projName = intent.ProjectName.Value;
        var pipelineName = pipelineOverride;
        if (string.IsNullOrWhiteSpace(pipelineName) && config2.Projects.TryGetValue(projName, out var pc2))
            pipelineName = pc2.Pipeline;

        return new PipelineRequest(
            projName,
            pipelineName ?? "fix-bug",
            TicketId: intent.TicketId,
            Headless: headless,
            Context: initialContext);
    }

    private void LogResult(CommandResult result, string projectName)
    {
        if (result.IsSuccess)
            logger.LogInformation(
                "Project {Project} processed successfully: {Message}",
                projectName, result.Message);
        else
            logger.LogWarning(
                "Project {Project} processing failed: {Message}",
                projectName, result.Message);
    }
}
