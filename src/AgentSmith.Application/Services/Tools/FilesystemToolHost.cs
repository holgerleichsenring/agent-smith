using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Sandbox filesystem tools (ReadFile/WriteFile/ListFiles/Grep/RunCommand).
/// Per-phase filter: read-only set in Review/Discuss/Filter/Synthesize;
/// +RunCommand in Plan/Investigate/Verify (per p0151a — Plan-phase recon
/// skills need run_command for directory inventory); all five in
/// Implementation; ReadFile+Grep+ListFiles+WriteFile (no RunCommand) in
/// Bootstrap; null phase → all five (legacy fallback).
/// </summary>
public sealed class FilesystemToolHost : IToolHost
{
    private readonly SandboxStepRunner _runner;
    private readonly string _repoPath;
    private readonly ToolGuardInvoker _guards;
    private readonly List<CodeChange> _changes = new();

    public FilesystemToolHost(
        ISandbox sandbox, string repoPath = "/work",
        IPathReadGuard? readGuard = null, IPathWriteGuard? writeGuard = null)
    {
        _runner = new SandboxStepRunner(sandbox);
        _repoPath = repoPath;
        _guards = new ToolGuardInvoker(readGuard, writeGuard);
    }

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = investigatorMode;
        return phase switch
        {
            null => All(),
            SkillExecutionPhase.Implementation => All(),
            SkillExecutionPhase.Bootstrap => BootstrapSet(),
            SkillExecutionPhase.Plan or SkillExecutionPhase.Verify or SkillExecutionPhase.Investigate => InvestigatorSet(),
            _ => ReadOnlySet()
        };
    }

    [Description("Reads the contents of a file at the given path.")]
    public Task<string> ReadFile(
        [Description("Repository-relative path to read.")] string path,
        CancellationToken ct = default)
        => _guards.CheckRead(path) is { } error
            ? Task.FromResult(error)
            : _runner.ReadAsync(path, ct);

    [Description("Writes the given content to a file at the given path. Overwrites if it exists.")]
    public async Task<string> WriteFile(
        [Description("Repository-relative path to write.")] string path,
        [Description("Full content to write to the file.")] string content,
        CancellationToken ct = default)
    {
        if (_guards.CheckWrite(path) is { } error) return error;
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
        => _guards.CheckRead(path) is { } error
            ? Task.FromResult(error)
            : _runner.ListAsync(path, maxDepth, ct);

    [Description("Searches files for a regular expression pattern under the given path.")]
    public Task<string> Grep(
        [Description("Regular expression pattern to search for.")] string pattern,
        [Description("Repository-relative path to search under.")] string path = ".",
        [Description("Optional glob filter (e.g. '*.cs').")] string? glob = null,
        [Description("Maximum number of matches to return (default 200).")] int? maxMatches = null,
        CancellationToken ct = default)
        => _guards.CheckRead(path) is { } error
            ? Task.FromResult(error)
            : _runner.GrepAsync(pattern, path, glob, maxMatches, ct);

    [Description("Runs a shell command. Long-running server processes are blocked.")]
    public Task<string> RunCommand(
        [Description("Shell command to execute (passed to /bin/sh -c).")] string command,
        CancellationToken ct = default)
        => _runner.RunAsync(command, ct);

    private IEnumerable<AIFunction> ReadOnlySet() =>
    [
        AIFunctionFactory.Create(ReadFile),
        AIFunctionFactory.Create(Grep),
        AIFunctionFactory.Create(ListFiles)
    ];

    private IEnumerable<AIFunction> InvestigatorSet() =>
    [
        AIFunctionFactory.Create(ReadFile),
        AIFunctionFactory.Create(Grep),
        AIFunctionFactory.Create(ListFiles),
        AIFunctionFactory.Create(RunCommand)
    ];

    private IEnumerable<AIFunction> BootstrapSet() =>
    [
        AIFunctionFactory.Create(ReadFile),
        AIFunctionFactory.Create(Grep),
        AIFunctionFactory.Create(ListFiles),
        AIFunctionFactory.Create(WriteFile)
    ];

    private IEnumerable<AIFunction> All() =>
    [
        AIFunctionFactory.Create(ReadFile),
        AIFunctionFactory.Create(WriteFile),
        AIFunctionFactory.Create(ListFiles),
        AIFunctionFactory.Create(Grep),
        AIFunctionFactory.Create(RunCommand)
    ];
}
