using System.ComponentModel;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Backwards-compatible facade over the three p0145 decomposed hosts
/// (FilesystemToolHost + LogDecisionToolHost + HumanToolHost). Preserves
/// the pre-p0145 surface that existing handlers (AgenticExecuteHandler,
/// BootstrapRoundHandler, GenerateDocsHandler, GenerateTestsHandler,
/// VerifyRoundHandler, ProjectAnalyzer) construct directly. New code
/// should compose <see cref="IToolKit"/> + <see cref="IToolHost"/>
/// instances via DI instead.
/// </summary>
[Obsolete("Use IToolKit + IPipelineToolPolicy. Will be removed after Message-Pipeline-Family is in production.")]
public sealed class SandboxToolHost
{
    private readonly FilesystemToolHost _fs;
    private readonly LogDecisionToolHost _log;
    private readonly HumanToolHost _human;

    public SandboxToolHost(
        ISandbox sandbox, IDecisionLogger decisionLogger,
        IDialogueTransport? dialogueTransport = null, string? jobId = null,
        string repoPath = "/work",
        IPathReadGuard? readGuard = null, IPathWriteGuard? writeGuard = null)
    {
        _fs = new FilesystemToolHost(sandbox, repoPath, readGuard, writeGuard);
        _log = new LogDecisionToolHost(decisionLogger, repoPath);
        _human = new HumanToolHost(dialogueTransport, jobId);
    }

    public IReadOnlyList<CodeChange> GetChanges() => _fs.GetChanges();
    public IReadOnlyList<PlanDecision> GetDecisions() => _log.GetDecisions();

    internal FilesystemToolHost Filesystem => _fs;
    internal LogDecisionToolHost LogDecisionHost => _log;
    internal HumanToolHost Human => _human;

    [Description("Reads the contents of a file at the given path.")]
    public Task<string> ReadFile(
        [Description("Repository-relative path to read.")] string path,
        CancellationToken ct = default) => _fs.ReadFile(path, ct);

    [Description("Writes the given content to a file at the given path. Overwrites if it exists.")]
    public Task<string> WriteFile(
        [Description("Repository-relative path to write.")] string path,
        [Description("Full content to write to the file.")] string content,
        CancellationToken ct = default) => _fs.WriteFile(path, content, ct);

    [Description("Lists files and folders under the given path.")]
    public Task<string> ListFiles(
        [Description("Repository-relative path to list. Use '.' for the repo root.")] string path = ".",
        [Description("Optional max depth to recurse.")] int? maxDepth = null,
        CancellationToken ct = default) => _fs.ListFiles(path, maxDepth, ct);

    [Description("Searches files for a regular expression pattern under the given path.")]
    public Task<string> Grep(
        [Description("Regular expression pattern to search for.")] string pattern,
        [Description("Repository-relative path to search under.")] string path = ".",
        [Description("Optional glob filter (e.g. '*.cs').")] string? glob = null,
        [Description("Maximum number of matches to return (default 200).")] int? maxMatches = null,
        CancellationToken ct = default) => _fs.Grep(pattern, path, glob, maxMatches, ct);

    [Description("Runs a shell command. Long-running server processes are blocked.")]
    public Task<string> RunCommand(
        [Description("Shell command to execute (passed to /bin/sh -c).")] string command,
        CancellationToken ct = default) => _fs.RunCommand(command, ct);

    [Description("Logs a key architectural, tooling, implementation, or trade-off decision.")]
    public Task<string> LogDecision(
        [Description("Category: Architecture, Tooling, Implementation, or TradeOff.")] string category,
        [Description("One-line description of the decision and its rationale.")] string decision,
        CancellationToken ct = default) => _log.LogDecision(category, decision, ct);

    [Description("Asks the human operator for guidance via the dialogue transport.")]
    public Task<string> AskHuman(
        [Description("Question text to display to the human.")] string question,
        [Description("Optional context block shown alongside the question.")] string? context = null,
        [Description("Optional list of choices for multiple-choice answers.")] IReadOnlyList<string>? choices = null,
        CancellationToken ct = default) => _human.AskHuman(question, context, choices, ct);
}
