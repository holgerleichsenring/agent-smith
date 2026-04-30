using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Executes tool calls from the AI agent against the local repository.
/// Dispatches to specialised handlers for file operations and human questions.
/// </summary>
public sealed class ToolExecutor(
    string repositoryPath,
    ILogger logger,
    FileReadTracker? fileReadTracker = null,
    IProgressReporter? progressReporter = null,
    IDialogueTransport? dialogueTransport = null,
    IDialogueTrail? dialogueTrail = null,
    string? jobId = null)
{
    private readonly CommandRunner _commandRunner = new(repositoryPath, logger, progressReporter);
    private readonly FileToolHandler _fileHandler = new(repositoryPath, logger, fileReadTracker, progressReporter);
    private readonly GrepToolHandler _grepHandler = new(repositoryPath, logger);
    private readonly HumanQuestionToolHandler _humanHandler = new(dialogueTransport, dialogueTrail, jobId, logger, progressReporter);
    private readonly List<PlanDecision> _decisions = new();

    public IReadOnlyList<CodeChange> GetChanges() => _fileHandler.GetChanges();
    public IReadOnlyList<PlanDecision> GetDecisions() => _decisions.AsReadOnly();

    public async Task<string> ExecuteAsync(string toolName, JsonNode? input)
    {
        try
        {
            return toolName switch
            {
                "read_file" => _fileHandler.ReadFile(input),
                "write_file" => _fileHandler.WriteFile(input),
                "list_files" => _fileHandler.ListFiles(input),
                "grep" => _grepHandler.Grep(input),
                "run_command" => await _commandRunner.RunAsync(input),
                "log_decision" => LogDecision(input),
                "ask_human" => await _humanHandler.HandleAsync(input),
                _ => $"Error: Unknown tool '{toolName}'."
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool execution failed: {Tool}", toolName);
            return $"Error: {ex.Message}";
        }
    }

    private string LogDecision(JsonNode? input)
    {
        var category = ToolParams.GetString(input, "category");
        var decision = ToolParams.GetString(input, "decision");

        _decisions.Add(new PlanDecision(category, decision));
        logger.LogDebug("Decision logged [{Category}]: {Decision}", category, decision);
        return $"Decision logged: [{category}] {decision}";
    }
}
