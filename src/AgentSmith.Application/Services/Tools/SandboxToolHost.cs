using System.ComponentModel;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Single source of truth for the LLM tool surface. Each public method becomes
/// an AIFunction via AIFunctionFactory.Create (see SandboxToolHostExtensions).
/// [Description] attributes drive the schema; method signatures drive the
/// parameter types. Replaces 4 provider-specific ToolDefinitions files.
/// </summary>
public sealed class SandboxToolHost
{
    private readonly SandboxStepRunner _runner;
    private readonly IDecisionLogger _decisionLogger;
    private readonly IDialogueTransport? _dialogueTransport;
    private readonly string? _jobId;
    private readonly string _repoPath;
    private readonly List<CodeChange> _changes = new();
    private readonly List<PlanDecision> _decisions = new();

    public SandboxToolHost(
        ISandbox sandbox, IDecisionLogger decisionLogger,
        IDialogueTransport? dialogueTransport = null, string? jobId = null,
        string repoPath = "/work")
    {
        _runner = new SandboxStepRunner(sandbox);
        _decisionLogger = decisionLogger;
        _dialogueTransport = dialogueTransport;
        _jobId = jobId;
        _repoPath = repoPath;
    }

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();
    public IReadOnlyList<PlanDecision> GetDecisions() => _decisions.AsReadOnly();

    [Description("Reads the contents of a file at the given path.")]
    public Task<string> ReadFile(
        [Description("Repository-relative path to read.")] string path,
        CancellationToken ct = default)
        => _runner.ReadAsync(path, ct);

    [Description("Writes the given content to a file at the given path. Overwrites if it exists.")]
    public async Task<string> WriteFile(
        [Description("Repository-relative path to write.")] string path,
        [Description("Full content to write to the file.")] string content,
        CancellationToken ct = default)
    {
        var result = await _runner.WriteAsync(path, content, ct);
        if (!result.StartsWith("Error", StringComparison.Ordinal))
            _changes.Add(new CodeChange(new FilePath(path), content, "Modify"));
        return result;
    }

    [Description("Lists files and folders under the given path.")]
    public Task<string> ListFiles(
        [Description("Repository-relative path to list. Use '.' for the repo root.")] string path = ".",
        [Description("Optional max depth to recurse.")] int? maxDepth = null,
        CancellationToken ct = default)
        => _runner.ListAsync(path, maxDepth, ct);

    [Description("Searches files for a regular expression pattern under the given path.")]
    public Task<string> Grep(
        [Description("Regular expression pattern to search for.")] string pattern,
        [Description("Repository-relative path to search under.")] string path = ".",
        [Description("Optional glob filter (e.g. '*.cs').")] string? glob = null,
        [Description("Maximum number of matches to return (default 200).")] int? maxMatches = null,
        CancellationToken ct = default)
        => _runner.GrepAsync(pattern, path, glob, maxMatches, ct);

    [Description("Runs a shell command. Long-running server processes are blocked.")]
    public Task<string> RunCommand(
        [Description("Shell command to execute (passed to /bin/sh -c).")] string command,
        CancellationToken ct = default)
        => _runner.RunAsync(command, ct);

    [Description("Logs a key architectural, tooling, implementation, or trade-off decision.")]
    public async Task<string> LogDecision(
        [Description("Category: Architecture, Tooling, Implementation, or TradeOff.")] string category,
        [Description("One-line description of the decision and its rationale.")] string decision,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<DecisionCategory>(category, ignoreCase: true, out var cat))
            return $"Error: invalid category '{category}'.";
        await _decisionLogger.LogAsync(_repoPath, cat, decision, ct);
        _decisions.Add(new PlanDecision(category, decision));
        return $"Decision logged: [{category}] {decision}";
    }

    [Description("Asks the human operator for guidance via the dialogue transport.")]
    public async Task<string> AskHuman(
        [Description("Question text to display to the human.")] string question,
        [Description("Optional context block shown alongside the question.")] string? context = null,
        [Description("Optional list of choices for multiple-choice answers.")] IReadOnlyList<string>? choices = null,
        CancellationToken ct = default)
    {
        if (_dialogueTransport is null || _jobId is null)
            return "Error: Dialogue transport not configured.";
        var qid = Guid.NewGuid().ToString("N");
        var qType = choices is { Count: > 0 } ? QuestionType.Choice : QuestionType.FreeText;
        var dq = new DialogQuestion(qid, qType, question, context, choices, "", TimeSpan.FromMinutes(5));
        await _dialogueTransport.PublishQuestionAsync(_jobId, dq, ct);
        var answer = await _dialogueTransport.WaitForAnswerAsync(_jobId, qid, TimeSpan.FromMinutes(5), ct);
        return answer is null ? "Answer (timeout): " : $"Answer: {answer.Answer}";
    }
}
