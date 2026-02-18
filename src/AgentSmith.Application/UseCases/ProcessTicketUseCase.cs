using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.UseCases;

/// <summary>
/// Central entry point: takes user input, parses intent, loads config,
/// resolves the project pipeline, and executes it end-to-end.
/// </summary>
public sealed class ProcessTicketUseCase(
    IConfigurationLoader configLoader,
    IIntentParser intentParser,
    IPipelineExecutor pipelineExecutor,
    ILogger<ProcessTicketUseCase> logger)
{
    public async Task<CommandResult> ExecuteAsync(
        string userInput,
        string configPath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing input: {Input}", userInput);

        var config = configLoader.LoadConfig(configPath);
        var intent = await intentParser.ParseAsync(userInput, cancellationToken);

        var projectName = intent.ProjectName.Value;
        if (!config.Projects.TryGetValue(projectName, out var projectConfig))
            throw new ConfigurationException(
                $"Project '{projectName}' not found in configuration.");

        var pipelineName = projectConfig.Pipeline;
        if (!config.Pipelines.TryGetValue(pipelineName, out var pipelineConfig))
            throw new ConfigurationException(
                $"Pipeline '{pipelineName}' not found in configuration.");

        logger.LogInformation(
            "Running pipeline '{Pipeline}' for project '{Project}', ticket {Ticket}",
            pipelineName, projectName, intent.TicketId);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, intent.TicketId);

        var result = await pipelineExecutor.ExecuteAsync(
            pipelineConfig.Commands, projectConfig, pipeline, cancellationToken);

        LogResult(result, intent);
        return result;
    }

    private void LogResult(CommandResult result, ParsedIntent intent)
    {
        if (result.Success)
        {
            logger.LogInformation(
                "Ticket {Ticket} processed successfully: {Message}",
                intent.TicketId, result.Message);
        }
        else
        {
            logger.LogWarning(
                "Ticket {Ticket} processing failed: {Message}",
                intent.TicketId, result.Message);
        }
    }
}
