using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services;

/// <summary>
/// Central entry point: takes user input, parses intent, loads config,
/// resolves the project pipeline, and executes it end-to-end.
/// Supports both ticket-based flows ("fix #123 in project") and
/// init flows ("init in project").
/// </summary>
public sealed class ProcessTicketUseCase(
    IConfigurationLoader configLoader,
    IIntentParser intentParser,
    IPipelineExecutor pipelineExecutor,
    ILogger<ProcessTicketUseCase> logger)
{
    private static readonly Regex InitPattern = new(
        @"^init\s+(?:in\s+)?(\S+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<CommandResult> ExecuteAsync(
        string userInput,
        string configPath,
        bool headless,
        string? pipelineOverride,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing input: {Input}", userInput);

        var initMatch = InitPattern.Match(userInput);
        if (initMatch.Success)
            return await ExecuteInitAsync(
                initMatch.Groups[1].Value, configPath, pipelineOverride, headless, cancellationToken);

        return await ExecuteTicketAsync(
            userInput, configPath, pipelineOverride, headless, cancellationToken);
    }

    private async Task<CommandResult> ExecuteTicketAsync(
        string userInput,
        string configPath,
        string? pipelineOverride,
        bool headless,
        CancellationToken cancellationToken)
    {
        var config = configLoader.LoadConfig(configPath);
        var intent = await intentParser.ParseAsync(userInput, cancellationToken);

        var projectName = intent.ProjectName.Value;
        if (!config.Projects.TryGetValue(projectName, out var projectConfig))
            throw new ConfigurationException(
                $"Project '{projectName}' not found in configuration.");

        var pipelineName = pipelineOverride ?? projectConfig.Pipeline;
        var commands = ResolvePipeline(pipelineName);

        logger.LogInformation(
            "Running pipeline '{Pipeline}' for project '{Project}', ticket {Ticket}",
            pipelineName, projectName, intent.TicketId);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, intent.TicketId);
        pipeline.Set(ContextKeys.Headless, headless);

        var result = await pipelineExecutor.ExecuteAsync(
            commands, projectConfig, pipeline, cancellationToken);

        if (result.IsSuccess && pipeline.TryGet<string>(ContextKeys.PullRequestUrl, out var prUrl))
            result = result with { PrUrl = prUrl };

        LogResult(result, projectName);
        return result;
    }

    private async Task<CommandResult> ExecuteInitAsync(
        string projectName,
        string configPath,
        string? pipelineOverride,
        bool headless,
        CancellationToken cancellationToken)
    {
        var config = configLoader.LoadConfig(configPath);

        projectName = projectName.ToLowerInvariant();
        if (!config.Projects.TryGetValue(projectName, out var projectConfig))
            throw new ConfigurationException(
                $"Project '{projectName}' not found in configuration.");

        var pipelineName = pipelineOverride ?? "init-project";
        var commands = ResolvePipeline(pipelineName);

        logger.LogInformation(
            "Running init pipeline '{Pipeline}' for project '{Project}'",
            pipelineName, projectName);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.InitMode, true);
        pipeline.Set(ContextKeys.Headless, headless);

        var result = await pipelineExecutor.ExecuteAsync(
            commands, projectConfig, pipeline, cancellationToken);

        if (result.IsSuccess && pipeline.TryGet<string>(ContextKeys.PullRequestUrl, out var prUrl))
            result = result with { PrUrl = prUrl };

        LogResult(result, projectName);
        return result;
    }

    private static IReadOnlyList<string> ResolvePipeline(string pipelineName)
    {
        return PipelinePresets.TryResolve(pipelineName)
            ?? throw new ConfigurationException(
                $"Pipeline '{pipelineName}' not found in presets.");
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
