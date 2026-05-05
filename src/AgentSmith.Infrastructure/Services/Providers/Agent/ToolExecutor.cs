using System.Text.Json.Nodes;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Executes tool calls from the AI agent. When constructed with an ISandbox,
/// read_file / write_file / list_files / run_command all delegate to the
/// sandbox via Wire-typed Steps. Without a sandbox, falls back to direct
/// repository-path access (used by ScoutAgent and legacy callers).
/// </summary>
public sealed class ToolExecutor
{
    private readonly ILogger _logger;
    private readonly SandboxToolHandler? _sandboxHandler;
    private readonly CommandRunner? _commandRunner;
    private readonly FileToolHandler? _fileHandler;
    private readonly GrepToolHandler _grepHandler;
    private readonly HumanQuestionToolHandler _humanHandler;
    private readonly List<PlanDecision> _decisions = new();

    public ToolExecutor(
        string repositoryPath,
        ILogger logger,
        FileReadTracker? fileReadTracker = null,
        IProgressReporter? progressReporter = null,
        IDialogueTransport? dialogueTransport = null,
        IDialogueTrail? dialogueTrail = null,
        string? jobId = null,
        ISandbox? sandbox = null)
    {
        _logger = logger;
        _grepHandler = new GrepToolHandler(repositoryPath, logger);
        _humanHandler = new HumanQuestionToolHandler(
            dialogueTransport, dialogueTrail, jobId, logger, progressReporter);

        if (sandbox is not null)
        {
            _sandboxHandler = new SandboxToolHandler(sandbox, logger, fileReadTracker);
        }
        else
        {
            _commandRunner = new CommandRunner(repositoryPath, logger, progressReporter);
            _fileHandler = new FileToolHandler(repositoryPath, logger, fileReadTracker, progressReporter);
        }
    }

    public IReadOnlyList<CodeChange> GetChanges() =>
        _sandboxHandler?.GetChanges() ?? _fileHandler!.GetChanges();

    public IReadOnlyList<PlanDecision> GetDecisions() => _decisions.AsReadOnly();

    public Task<string> ExecuteAsync(string toolName, JsonNode? input) =>
        ExecuteAsync(toolName, input, CancellationToken.None);

    public async Task<string> ExecuteAsync(string toolName, JsonNode? input, CancellationToken cancellationToken)
    {
        try
        {
            return toolName switch
            {
                "read_file" => await ReadFileAsync(input, cancellationToken),
                "write_file" => await WriteFileAsync(input, cancellationToken),
                "list_files" => await ListFilesAsync(input, cancellationToken),
                "grep" => _grepHandler.Grep(input),
                "run_command" => await RunCommandAsync(input, cancellationToken),
                "log_decision" => LogDecision(input),
                "ask_human" => await _humanHandler.HandleAsync(input),
                _ => $"Error: Unknown tool '{toolName}'."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tool execution failed: {Tool}", toolName);
            return $"Error: {ex.Message}";
        }
    }

    private Task<string> ReadFileAsync(JsonNode? input, CancellationToken ct) =>
        _sandboxHandler is not null
            ? _sandboxHandler.ReadFileAsync(input, ct)
            : Task.FromResult(_fileHandler!.ReadFile(input));

    private Task<string> WriteFileAsync(JsonNode? input, CancellationToken ct) =>
        _sandboxHandler is not null
            ? _sandboxHandler.WriteFileAsync(input, ct)
            : Task.FromResult(_fileHandler!.WriteFile(input));

    private Task<string> ListFilesAsync(JsonNode? input, CancellationToken ct) =>
        _sandboxHandler is not null
            ? _sandboxHandler.ListFilesAsync(input, ct)
            : Task.FromResult(_fileHandler!.ListFiles(input));

    private Task<string> RunCommandAsync(JsonNode? input, CancellationToken ct) =>
        _sandboxHandler is not null
            ? _sandboxHandler.RunCommandAsync(input, ct)
            : _commandRunner!.RunAsync(input);

    private string LogDecision(JsonNode? input)
    {
        var category = ToolParams.GetString(input, "category");
        var decision = ToolParams.GetString(input, "decision");

        _decisions.Add(new PlanDecision(category, decision));
        _logger.LogDebug("Decision logged [{Category}]: {Decision}", category, decision);
        return $"Decision logged: [{category}] {decision}";
    }
}
