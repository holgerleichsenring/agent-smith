using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Sandbox filesystem tools (ReadFile/WriteFile/ListFiles/Grep/RunCommand).
/// Per-phase filter: every phase set includes RunCommand — the LLM benefits
/// from raw shell access (find/grep/head/wc/curl) at every stage. A
/// destructive-command blocklist (<see cref="CommandGuard"/>) is applied
/// inside RunCommand as defense-in-depth on top of the sandbox boundary.
/// Write-capable sets (Bootstrap/Implementation) additionally include
/// WriteFile.
/// </summary>
public sealed class FilesystemToolHost : IToolHost
{
    private readonly SandboxStepRunner _runner;
    private readonly string _repoPath;
    private readonly ToolGuardInvoker _guards;
    private readonly ILogger? _logger;
    private readonly List<CodeChange> _changes = new();

    public FilesystemToolHost(
        ISandbox sandbox, string repoPath = "/work",
        IPathReadGuard? readGuard = null, IPathWriteGuard? writeGuard = null,
        ILogger? logger = null)
    {
        _runner = new SandboxStepRunner(sandbox);
        _repoPath = repoPath;
        _guards = new ToolGuardInvoker(readGuard, writeGuard);
        _logger = logger;
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
    {
        _logger?.LogInformation("tool_call: ReadFile path={Path}", path);
        return _guards.CheckRead(path) is { } error
            ? Task.FromResult(error)
            : _runner.ReadAsync(path, ct);
    }

    [Description("Writes the given content to a file at the given path. Overwrites if it exists.")]
    public async Task<string> WriteFile(
        [Description("Repository-relative path to write.")] string path,
        [Description("Full content to write to the file.")] string content,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: WriteFile path={Path} bytes={Bytes}", path, content.Length);
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
    {
        _logger?.LogInformation("tool_call: ListFiles path={Path} maxDepth={MaxDepth}", path, maxDepth);
        return _guards.CheckRead(path) is { } error
            ? Task.FromResult(error)
            : _runner.ListAsync(path, maxDepth, ct);
    }

    [Description("Lists files matching a glob pattern. Pattern is matched against file names with -name semantics; pass '**/' prefix to indicate recursion (default is already recursive). Examples: '*.cs', '**/*.json', 'Program.cs'. Returns up to 200 matches.")]
    public Task<string> Glob(
        [Description("Glob pattern, e.g. '*.cs' or 'Program.cs'.")] string pattern,
        [Description("Repository-relative base path to search from (default '.').")] string path = ".",
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: Glob pattern={Pattern} path={Path}", pattern, path);
        if (_guards.CheckRead(path) is { } error) return Task.FromResult(error);
        var namePattern = pattern.StartsWith("**/", StringComparison.Ordinal) ? pattern[3..] : pattern;
        var cmd = $"find {ShellQuote(path)} -type f -name {ShellQuote(namePattern)} 2>/dev/null | head -200";
        return _runner.RunAsync(cmd, ct);
    }

    [Description("Replaces an EXACT string occurrence in a file. old_string must appear EXACTLY ONCE in the file or the call fails — provide enough surrounding context to make it unique. Use this for targeted edits instead of WriteFile (which overwrites the whole file).")]
    public async Task<string> Edit(
        [Description("Repository-relative path to edit.")] string path,
        [Description("Exact string to find. Must appear exactly once in the file. Include enough surrounding context for uniqueness.")] string old_string,
        [Description("Replacement string.")] string new_string,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: Edit path={Path} old_len={Old} new_len={New}", path, old_string?.Length ?? 0, new_string?.Length ?? 0);
        if (_guards.CheckRead(path) is { } readErr) return readErr;
        if (_guards.CheckWrite(path) is { } writeErr) return writeErr;
        if (string.IsNullOrEmpty(old_string)) return "Error: old_string must not be empty.";

        var content = await _runner.ReadAsync(path, ct);
        if (content.StartsWith("Error", StringComparison.Ordinal)) return content;

        var idx = content.IndexOf(old_string, StringComparison.Ordinal);
        if (idx < 0) return $"Error: old_string not found in {path}.";
        if (content.IndexOf(old_string, idx + 1, StringComparison.Ordinal) >= 0)
            return $"Error: old_string appears multiple times in {path} — extend the snippet with surrounding context to make it unique.";

        var newContent = string.Concat(content.AsSpan(0, idx), new_string, content.AsSpan(idx + old_string.Length));
        var result = await _runner.WriteAsync(path, newContent, ct);
        if (!result.StartsWith("Error", StringComparison.Ordinal))
            _changes.Add(new CodeChange(new FilePath(path), newContent, "Modify"));
        return result;
    }

    [Description("Sends an HTTP request from inside the sandbox and returns response status, headers, and body. Use for live API probing (e.g. anonymous endpoint behavior, malformed payload response codes, header inspection) when a target URL is reachable. Built on `curl -sS -i` under the hood.")]
    public Task<string> HttpRequest(
        [Description("HTTP method: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.")] string method,
        [Description("Full URL including scheme.")] string url,
        [Description("Optional request body for POST/PUT/PATCH. Empty for GET/HEAD/DELETE/OPTIONS.")] string? body = null,
        [Description("Optional request headers, one per line: 'Authorization: Bearer xyz\\nContent-Type: application/json'.")] string? headers = null,
        [Description("Optional connection timeout in seconds (default 15, max 60).")] int? timeoutSeconds = null,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: HttpRequest {Method} {Url} body_len={BodyLen}", method, url, body?.Length ?? 0);

        var allowedMethods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };
        var upperMethod = method?.ToUpperInvariant() ?? "GET";
        if (!allowedMethods.Contains(upperMethod))
            return Task.FromResult($"Error: unsupported HTTP method '{method}'. Allowed: {string.Join(", ", allowedMethods)}.");

        if (string.IsNullOrWhiteSpace(url))
            return Task.FromResult("Error: url is required.");

        var clampedTimeout = Math.Clamp(timeoutSeconds ?? 15, 1, 60);
        var parts = new List<string> { "curl", "-sS", "-i", "--max-time", clampedTimeout.ToString(), "-X", upperMethod };
        if (!string.IsNullOrEmpty(headers))
        {
            foreach (var line in headers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                parts.Add("-H");
                parts.Add(line);
            }
        }
        if (!string.IsNullOrEmpty(body))
        {
            parts.Add("--data-raw");
            parts.Add(body);
        }
        parts.Add(url);

        var cmd = string.Join(" ", parts.Select(ShellQuote));
        return _runner.RunAsync(cmd, ct);
    }

    private static string ShellQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    [Description("Searches files for a regular expression pattern under the given path.")]
    public Task<string> Grep(
        [Description("Regular expression pattern to search for.")] string pattern,
        [Description("Repository-relative path to search under.")] string path = ".",
        [Description("Optional glob filter (e.g. '*.cs').")] string? glob = null,
        [Description("Maximum number of matches to return (default 200).")] int? maxMatches = null,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: Grep pattern={Pattern} path={Path} glob={Glob}", pattern, path, glob);
        return _guards.CheckRead(path) is { } error
            ? Task.FromResult(error)
            : _runner.GrepAsync(pattern, path, glob, maxMatches, ct);
    }

    [Description("Runs a shell command (passed to /bin/sh -c). Destructive commands (rm/rmdir/unlink/shred/truncate/dd, raw device writes, fork bombs) are blocked by a defense-in-depth guard on top of the sandbox boundary; use find/grep/head/wc/curl/ls/cat freely.")]
    public Task<string> RunCommand(
        [Description("Shell command to execute (passed to /bin/sh -c).")] string command,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: RunCommand cmd={Command}", command);
        if (CommandGuard.Check(command) is { } error)
        {
            _logger?.LogWarning("RunCommand blocked by destructive-command guard: {Command}", command);
            return Task.FromResult(error);
        }
        return _runner.RunAsync(command, ct);
    }

    private static AIFunction Tool(Delegate impl, string name) =>
        AIFunctionFactory.Create(impl, name: name);

    private IEnumerable<AIFunction> ReadOnlySet() =>
    [
        Tool(ReadFile, "read_file"),
        Tool(Grep, "grep"),
        Tool(Glob, "glob"),
        Tool(ListFiles, "list_files"),
        Tool(RunCommand, "run_command"),
        Tool(HttpRequest, "http_request")
    ];

    private IEnumerable<AIFunction> InvestigatorSet() => ReadOnlySet();

    private IEnumerable<AIFunction> BootstrapSet() =>
    [
        Tool(ReadFile, "read_file"),
        Tool(Grep, "grep"),
        Tool(Glob, "glob"),
        Tool(ListFiles, "list_files"),
        Tool(WriteFile, "write_file"),
        Tool(Edit, "edit"),
        Tool(RunCommand, "run_command"),
        Tool(HttpRequest, "http_request")
    ];

    private IEnumerable<AIFunction> All() =>
    [
        Tool(ReadFile, "read_file"),
        Tool(WriteFile, "write_file"),
        Tool(Edit, "edit"),
        Tool(ListFiles, "list_files"),
        Tool(Grep, "grep"),
        Tool(Glob, "glob"),
        Tool(RunCommand, "run_command"),
        Tool(HttpRequest, "http_request")
    ];
}
