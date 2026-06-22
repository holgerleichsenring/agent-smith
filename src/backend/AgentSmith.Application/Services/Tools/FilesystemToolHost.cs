using System.ComponentModel;
using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Sandbox filesystem + shell tools. Schemas mirror MCP filesystem-server
/// where they exist (read/write/list/edit/tree) and Claude Code's grep + bash
/// shapes where MCP is silent. Every parameter list is flat + primitive-typed
/// so OpenAI strict mode and small Ollama tool calling both accept the
/// generated JSON schema.
///
/// Per-phase filter: Implementation gets the full surface; Bootstrap drops
/// run_command + http_request; Plan/Verify/Investigate get the read-only
/// subset (read_file, grep_in_*, find_files, list_directory, directory_tree,
/// run_command — destructive commands blocked by CommandGuard).
///
/// p0154 removed the deprecated grep / glob / list_files aliases in lockstep
/// with the agent-smith-skills 2.6.0 SKILL.md rename. Skills must use
/// grep_in_file / grep_in_tree / find_files / list_directory directly.
/// </summary>
public sealed class FilesystemToolHost : IToolHost
{
    private readonly IReadOnlyDictionary<string, SandboxStepRunner> _runners;
    private readonly string _defaultRepo;
    private readonly ToolGuardInvoker _guards;
    private readonly ILogger? _logger;
    // p0280: the master shares ONE FilesystemToolHost across concurrent sub-agents, so
    // the read-set + change list must be thread-safe. _sync guards both.
    private readonly object _sync = new();
    private readonly List<CodeChange> _changes = new();
    // p0279: every path the agent successfully read this run, raw as the agent
    // addressed it. Consumed by the scan findings-anchor (a finding claiming
    // analyzed_from_source on a file never in this set is downgraded to potential)
    // and by the coverage re-drive (too few distinct reads → re-prompt).
    private readonly HashSet<string> _readPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>p0279: distinct paths successfully read this run (raw, agent-addressed).</summary>
    public IReadOnlyCollection<string> ReadPaths { get { lock (_sync) return _readPaths.ToArray(); } }

    // Single-sandbox constructor — kept for back-compat with construction
    // sites that don't yet read the Sandboxes dict from PipelineContext.
    // Wraps the one sandbox in an empty-keyed dictionary so routing has a
    // sensible default ("" matches any unprefixed path).
    public FilesystemToolHost(
        ISandbox sandbox, string repoPath = "/work",
        IPathReadGuard? readGuard = null, IPathWriteGuard? writeGuard = null,
        SkillExecutionPhase writePhase = SkillExecutionPhase.Implementation,
        string? contextName = null,
        ILogger? logger = null,
        int? runCommandTimeoutSeconds = null)
        : this(
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [string.Empty] = sandbox },
            defaultRepo: string.Empty,
            repoPath, readGuard, writeGuard, writePhase, contextName, logger, runCommandTimeoutSeconds)
    { }

    // Multi-sandbox constructor (p0158e). Each tool call's first path segment
    // is checked against the dictionary keys; matches dispatch the bare
    // (prefix-stripped) path to that repo's sandbox. Unprefixed paths or
    // single-entry dicts dispatch to defaultRepo. run_command + http_request
    // also dispatch to defaultRepo unless overridden via the `repo` argument.
    public FilesystemToolHost(
        IReadOnlyDictionary<string, ISandbox> sandboxes,
        string defaultRepo,
        string repoPath = "/work",
        IPathReadGuard? readGuard = null, IPathWriteGuard? writeGuard = null,
        SkillExecutionPhase writePhase = SkillExecutionPhase.Implementation,
        string? contextName = null,
        ILogger? logger = null,
        int? runCommandTimeoutSeconds = null,
        IReadOnlyDictionary<string, string>? keyToRepo = null)
    {
        var runners = sandboxes.ToDictionary(
            kv => kv.Key, kv => new SandboxStepRunner(kv.Value, runCommandTimeoutSeconds),
            StringComparer.Ordinal);
        // p0250: alias each REPO NAME to its representative sandbox via the
        // authoritative key→repo map (ContextKeys.SandboxRepos). The master now
        // addresses by repo name (the stable identifier the prompt lists), and it
        // resolves to the SAME sandbox CommitAndPR's repo-name lookup uses — one
        // denominator. Closes the key-vs-name split p0180's toolchain-group keying
        // (`<repo>-<langSlug>`) opened: the agent's write and the commit no longer
        // disagree about which sandbox is "this repo".
        if (keyToRepo is not null)
            foreach (var (key, repoName) in keyToRepo)
                if (runners.TryGetValue(key, out var runner) && !runners.ContainsKey(repoName))
                    runners[repoName] = runner;
        _runners = runners;
        _defaultRepo = defaultRepo;
        _guards = new ToolGuardInvoker(readGuard, writeGuard, repoPath, writePhase, contextName);
        _logger = logger;
    }

    // p0259b: routing failures are RECOVERABLE tool errors, not run-aborting
    // exceptions. A throw here propagates through FunctionInvokingChatClient and
    // kills the entire master mid-run (observed: the agent explored a bare
    // '.agentsmith' path on a multi-repo project, and the InvalidOperationException
    // failed the whole api-scan at the master step). Returning an error STRING —
    // the same contract the read/write guards above use — lets the LLM see the
    // mistake and retry with a repo prefix. Runner is non-null exactly when Error
    // is null.
    private (SandboxStepRunner? Runner, string BarePath, string? Error) Route(string? path)
    {
        // p0179h: accept the multi-repo prefix form even in single-sandbox mode.
        // The master prompt teaches "always prefix paths with the repo name" so
        // strict single-repo mode would reject the prompt's own examples. When
        // the prefix matches the only repo, strip it; otherwise fall through to
        // pass-through (lets operators paste bare paths in single-repo CLI runs).
        var idx = path?.IndexOf('/') ?? -1;
        var first = idx < 0 ? path ?? string.Empty : path![..idx];
        if (_runners.Count <= 1)
        {
            if (_runners.TryGetValue(first, out var only))
            {
                var bare = idx < 0 ? "." : path![(idx + 1)..];
                return (only, string.IsNullOrEmpty(bare) ? "." : bare, null);
            }
            return (_runners[_defaultRepo], path ?? string.Empty, null);
        }
        if (_runners.TryGetValue(first, out var runner))
        {
            var bare = idx < 0 ? "." : path![(idx + 1)..];
            return (runner, string.IsNullOrEmpty(bare) ? "." : bare, null);
        }
        return (null, string.Empty,
            $"Error: path '{path}' must start with a repo name on a multi-repo project. " +
            $"Prefix it with one of the known repos, e.g. '{FirstRepoName()}/{path}'. " +
            $"Known repos: [{KnownRepoList()}].");
    }

    private (SandboxStepRunner? Runner, string? Error) Resolve(string? repo)
    {
        if (string.IsNullOrEmpty(repo))
            return (_runners[_defaultRepo], null);
        if (_runners.TryGetValue(repo, out var runner)) return (runner, null);
        return (null,
            $"Error: unknown repo '{repo}'. Known repos: [{KnownRepoList()}].");
    }

    private string KnownRepoList() => string.Join(", ", _runners.Keys.Where(k => k.Length > 0));
    private string FirstRepoName() => _runners.Keys.FirstOrDefault(k => k.Length > 0) ?? _defaultRepo;

    public IReadOnlyList<CodeChange> GetChanges() { lock (_sync) return _changes.ToList(); }

    // p0193: writes to .agentsmith/contexts/<name>/context.yaml are rejected
    // here and must go through write_context_yaml. The typed write path
    // emits YAML through the same serializer the parser uses, so the LLM
    // can never produce a context.yaml the framework cannot read back.
    private const string ContextYamlPathRejection =
        "Error: write_file / edit / multi_edit are not allowed for " +
        ".agentsmith/contexts/*/context.yaml. Use the write_context_yaml " +
        "tool — it takes a structured JSON document and emits valid YAML " +
        "via the framework's typed serializer, eliminating LLM-generated " +
        "YAML scanner failures.";

    private static bool IsContextYamlPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var normalised = path.Replace('\\', '/');
        var idx = normalised.IndexOf(".agentsmith/contexts/", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return false;
        return normalised.EndsWith("/context.yaml", StringComparison.OrdinalIgnoreCase);
    }

    // Dedup by path: the LLM frequently makes several edits to the same file in one run.
    // Downstream consumers (commit comment, run-result, log "N files changed") expect a per-file set.
    private void RecordChange(string path, string content)
    {
        var change = new CodeChange(new FilePath(path), content, "Modify");
        lock (_sync)
        {
            var idx = _changes.FindIndex(c => c.Path.Value == path);
            if (idx >= 0) _changes[idx] = change;
            else _changes.Add(change);
        }
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = investigatorMode;
        return phase switch
        {
            null => All(),
            SkillExecutionPhase.Implementation => All(),
            SkillExecutionPhase.Bootstrap => BootstrapSet(),
            SkillExecutionPhase.BootstrapDiscover => BootstrapDiscoverSet(),
            SkillExecutionPhase.Plan or SkillExecutionPhase.Verify or SkillExecutionPhase.Investigate => InvestigatorSet(),
            _ => ReadOnlySet()
        };
    }

    [Description("Reads a file. Optional start_line (1-based) and line_count select a slice; omit both to read the whole file. By default each line is prefixed with its number and a tab so you can cite file:line directly; pass with_line_numbers=false to get bare content.")]
    public async Task<string> ReadFile(
        [Description("Repository-relative path to read.")] string path,
        [Description("Optional 1-based starting line. Omit to read from the beginning.")] int? start_line = null,
        [Description("Optional count of lines to return from start_line. Omit to read to the end.")] int? line_count = null,
        [Description("When true (default), each output line is prefixed with its line number and a tab.")] bool with_line_numbers = true,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: ReadFile path={Path} start={Start} count={Count}", path, start_line, line_count);
        if (_guards.CheckRead(path) is { } error) return error;
        var (runner, bare, routeErr) = Route(path);
        if (routeErr is not null) return routeErr;
        var result = await runner!.ReadAsync(bare, start_line, line_count, with_line_numbers, ct);
        // p0279: record only successful reads (the runner returns an "Error…" string on failure).
        if (!string.IsNullOrEmpty(path) && !result.StartsWith("Error", StringComparison.Ordinal))
            lock (_sync) _readPaths.Add(path);
        return result;
    }

    [Description("Writes the given content to a file at the given path. Overwrites if it exists.")]
    public async Task<string> WriteFile(
        [Description("Repository-relative path to write.")] string path,
        [Description("Full content to write to the file.")] string content,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: WriteFile path={Path} bytes={Bytes}", path, content.Length);
        if (IsContextYamlPath(path))
            return "Error: write_file is not allowed for .agentsmith/contexts/*/context.yaml. " +
                   "Use the write_context_yaml tool instead — it takes a structured JSON " +
                   "document and emits valid YAML via the framework's typed serializer.";
        if (_guards.CheckWrite(path) is { } error) return error;
        var (runner, bare, routeErr) = Route(path);
        if (routeErr is not null) return routeErr;
        var result = await runner!.WriteAsync(bare, content, ct);
        if (!result.StartsWith("Error", StringComparison.Ordinal))
            RecordChange(path, content);
        return result;
    }

    [Description("Lists the contents of a directory. Pass depth>1 for recursive listing. with_sizes=true adds a size column; sort_by={name|size|mtime} controls ordering. Use this for ad-hoc inspection; use directory_tree for a nested overview, find_files when you already know a name pattern.")]
    public Task<string> ListDirectory(
        [Description("Repository-relative directory path. Use '.' for the repo root.")] string path = ".",
        [Description("Recursion depth (1 = direct children only). Omit for direct children only.")] int? depth = null,
        [Description("When true, include a size column. Default false.")] bool with_sizes = false,
        [Description("Sort order: 'name' (default), 'size' (descending), or 'mtime' (newest first).")] string sort_by = "name",
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: ListDirectory path={Path} depth={Depth} sizes={Sizes} sort={Sort}", path, depth, with_sizes, sort_by);
        if (_guards.CheckRead(path) is { } error) return Task.FromResult(error);
        var sort = ParseSortBy(sort_by);
        var (runner, bare, routeErr) = Route(path);
        if (routeErr is not null) return Task.FromResult(routeErr);
        return runner!.ListAsync(bare, depth, with_sizes, sort, ct);
    }

    private static DirectorySortBy ParseSortBy(string s) => s?.ToLowerInvariant() switch
    {
        "size" => DirectorySortBy.Size,
        "mtime" => DirectorySortBy.Mtime,
        _ => DirectorySortBy.Name
    };

    [Description("Returns a nested text tree of the given directory, MCP filesystem-server directory_tree shape. Use this to get a one-shot layout overview instead of N list_directory walks.")]
    public Task<string> DirectoryTree(
        [Description("Repository-relative root directory. Use '.' for the repo root.")] string root = ".",
        [Description("Maximum recursion depth (default 4).")] int? max_depth = null,
        [Description("Optional list of basename globs to skip (e.g. ['*.min.js', 'bin']). The standard noisy dirs (.git, node_modules, bin, obj, etc.) are always skipped.")] IReadOnlyList<string>? exclude_globs = null,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: DirectoryTree root={Root} depth={Depth}", root, max_depth);
        if (_guards.CheckRead(root) is { } error) return Task.FromResult(error);
        var (runner, bare, routeErr) = Route(root);
        if (routeErr is not null) return Task.FromResult(routeErr);
        return runner!.TreeAsync(bare, max_depth, exclude_globs, ct);
    }

    [Description("Finds files whose path (relative to root) matches a glob pattern. Pattern is path-relative — 'Controller.cs' matches any file with 'Controller.cs' in its path; '*.cs' matches every .cs file; 'src/**/*.cs' matches every .cs under src/. Truncates at head_limit and appends a marker line.")]
    public async Task<string> FindFiles(
        [Description("Glob pattern matched against the path relative to root. Substring match if no glob chars are present.")] string pattern,
        [Description("Repository-relative directory to search under (default '.').")] string root = ".",
        [Description("Maximum number of matches to return (default 1000).")] int? head_limit = null,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: FindFiles pattern={Pattern} root={Root}", pattern, root);
        if (_guards.CheckRead(root) is { } error) return error;
        var limit = head_limit ?? SizeLimits.GrepDefaultHeadLimit;
        var normalized = NormalizeFindPattern(pattern);
        var (runner, bareRoot, routeErr) = Route(root);
        if (routeErr is not null) return routeErr;
        // head -N+1 so we can detect over-limit and emit the truncation marker.
        var cmd = $"find {ShellQuote(bareRoot)} -type f -path {ShellQuote(normalized)} 2>/dev/null | head -{limit + 1}";
        var structured = await runner!.RunAsync(cmd, timeoutSeconds: null, ct);
        return FormatFindOutput(structured, limit);
    }

    private static string NormalizeFindPattern(string pattern)
    {
        var normalized = pattern.Replace("**", "*");
        var hasGlob = normalized.Contains('*') || normalized.Contains('?');
        return hasGlob ? normalized : $"*{normalized}*";
    }

    private static string FormatFindOutput(string structured, int limit)
    {
        // RunAsync returns the labeled-section format; pull stdout out.
        var stdout = ExtractStdoutSection(structured);
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length <= limit) return string.Join('\n', lines);
        return string.Join('\n', lines.Take(limit)) + $"\n(truncated: {limit} matches)";
    }

    private static string ExtractStdoutSection(string structured)
    {
        var idx = structured.IndexOf("stdout:\n", StringComparison.Ordinal);
        if (idx < 0) return string.Empty;
        var start = idx + "stdout:\n".Length;
        var end = structured.IndexOf("\n\nstderr:", start, StringComparison.Ordinal);
        return end < 0 ? structured[start..] : structured[start..end];
    }

    [Description("Replaces an EXACT string occurrence in a file. By default old_string must appear EXACTLY ONCE — provide enough surrounding context to make it unique. With replace_all=true, every occurrence is replaced and the count is reported.")]
    public async Task<string> Edit(
        [Description("Repository-relative path to edit.")] string path,
        [Description("Exact string to find. Include enough surrounding context for uniqueness unless replace_all is true.")] string old_string,
        [Description("Replacement string.")] string new_string,
        [Description("When true, replace every occurrence and report the count. Default false.")] bool replace_all = false,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: Edit path={Path} old_len={Old} new_len={New} replace_all={All}",
            path, old_string?.Length ?? 0, new_string?.Length ?? 0, replace_all);
        if (IsContextYamlPath(path))
            return ContextYamlPathRejection;
        if (_guards.CheckRead(path) is { } readErr) return readErr;
        if (_guards.CheckWrite(path) is { } writeErr) return writeErr;
        if (string.IsNullOrEmpty(old_string)) return "Error: old_string must not be empty.";

        var (runner, bare, routeErr) = Route(path);
        if (routeErr is not null) return routeErr;
        var content = await runner!.ReadAsync(bare, startLine: null, lineCount: null, withLineNumbers: false, ct);
        if (content.StartsWith("Error", StringComparison.Ordinal)) return content;

        // A null new_string is a deletion of old_string — treat it as empty.
        var (newContent, count, error) = ApplyEdit(content, old_string, new_string ?? string.Empty, replace_all, path);
        if (error is not null) return error;
        var result = await runner.WriteAsync(bare, newContent, ct);
        if (!result.StartsWith("Error", StringComparison.Ordinal))
            RecordChange(path, newContent);
        return replace_all
            ? $"{result}\nReplaced {count} occurrence(s)."
            : result;
    }

    private static (string Content, int Count, string? Error) ApplyEdit(
        string content, string oldString, string newString, bool replaceAll, string path)
    {
        var idx = content.IndexOf(oldString, StringComparison.Ordinal);
        if (idx < 0) return (content, 0, $"Error: old_string not found in {path}.");
        if (!replaceAll && content.IndexOf(oldString, idx + 1, StringComparison.Ordinal) >= 0)
            return (content, 0, $"Error: old_string appears multiple times in {path} — extend the snippet with surrounding context to make it unique, or pass replace_all=true.");
        if (!replaceAll)
        {
            var single = string.Concat(content.AsSpan(0, idx), newString, content.AsSpan(idx + oldString.Length));
            return (single, 1, null);
        }
        // Manual replace_all so we can count and avoid Regex pitfalls with special chars.
        var sb = new StringBuilder(content.Length);
        var pos = 0;
        var count = 0;
        while (pos < content.Length)
        {
            var next = content.IndexOf(oldString, pos, StringComparison.Ordinal);
            if (next < 0) { sb.Append(content, pos, content.Length - pos); break; }
            sb.Append(content, pos, next - pos);
            sb.Append(newString);
            pos = next + oldString.Length;
            count++;
        }
        return (sb.ToString(), count, null);
    }

    [Description("Atomically applies multiple text edits to a single file. All edits succeed or none apply. Useful for a refactor or rename across one file in one round-trip. With dry_run=true, returns a per-edit summary and writes nothing.")]
    public async Task<string> MultiEdit(
        [Description("Repository-relative path to edit.")] string path,
        [Description("Ordered list of edits to apply. Each edit is {old_string, new_string, replace_all (optional, default false)}.")] IReadOnlyList<MultiEditOp> edits,
        [Description("When true, simulate the edits and return a summary without writing.")] bool dry_run = false,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: MultiEdit path={Path} edits={Count} dry_run={Dry}", path, edits?.Count ?? 0, dry_run);
        if (edits is null || edits.Count == 0) return "Error: edits must contain at least one entry.";
        if (IsContextYamlPath(path))
            return ContextYamlPathRejection;
        if (_guards.CheckRead(path) is { } readErr) return readErr;
        if (_guards.CheckWrite(path) is { } writeErr) return writeErr;

        var (runner, bare, routeErr) = Route(path);
        if (routeErr is not null) return routeErr;
        var content = await runner!.ReadAsync(bare, startLine: null, lineCount: null, withLineNumbers: false, ct);
        if (content.StartsWith("Error", StringComparison.Ordinal)) return content;

        var summary = new StringBuilder();
        for (var i = 0; i < edits.Count; i++)
        {
            var op = edits[i];
            if (string.IsNullOrEmpty(op.old_string)) return $"Error: edit #{i + 1} old_string must not be empty.";
            var (next, count, err) = ApplyEdit(content, op.old_string, op.new_string ?? string.Empty, op.replace_all, path);
            if (err is not null) return $"Error in edit #{i + 1}: {err[7..]}";
            summary.AppendLine($"edit #{i + 1}: {count} replacement(s)");
            content = next;
        }

        if (dry_run) return $"dry_run: would apply {edits.Count} edit(s) to {path}\n{summary}";

        var result = await runner.WriteAsync(bare, content, ct);
        if (!result.StartsWith("Error", StringComparison.Ordinal))
            RecordChange(path, content);
        return $"{result}\nApplied {edits.Count} edit(s).\n{summary}";
    }

    /// <summary>Per-edit operation for <see cref="MultiEdit"/>. Flat record so the
    /// generated JSON schema is OpenAI-strict-mode safe and Ollama-friendly.</summary>
    public sealed record MultiEditOp(
        [property: Description("Exact string to find.")] string old_string,
        [property: Description("Replacement string.")] string new_string,
        [property: Description("When true, replace every occurrence in this step. Default false.")] bool replace_all = false);

    [Description("Sends an HTTP request from inside the sandbox and returns response status, headers, and body. Use for live API probing (anonymous endpoint behaviour, malformed-payload response codes, header inspection). For reading documentation pages, p0154 ships web_fetch.")]
    public Task<string> HttpRequest(
        [Description("HTTP method: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS.")] string method,
        [Description("Full URL including scheme.")] string url,
        [Description("Optional request body for POST/PUT/PATCH.")] string? body = null,
        [Description("Optional request headers, one per line: 'Authorization: Bearer xyz\\nContent-Type: application/json'.")] string? headers = null,
        [Description("Optional connection timeout in seconds (default 15, max 60).")] int? timeout_seconds = null,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: HttpRequest {Method} {Url} body_len={BodyLen}", method, url, body?.Length ?? 0);
        var allowedMethods = new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };
        var upperMethod = method?.ToUpperInvariant() ?? "GET";
        if (!allowedMethods.Contains(upperMethod))
            return Task.FromResult($"Error: unsupported HTTP method '{method}'. Allowed: {string.Join(", ", allowedMethods)}.");
        if (string.IsNullOrWhiteSpace(url))
            return Task.FromResult("Error: url is required.");

        var clampedTimeout = Math.Clamp(timeout_seconds ?? 15, 1, 60);
        var parts = new List<string> { "curl", "-sS", "-i", "--max-time", clampedTimeout.ToString(), "-X", upperMethod };
        if (!string.IsNullOrEmpty(headers))
            foreach (var line in headers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            { parts.Add("-H"); parts.Add(line); }
        if (!string.IsNullOrEmpty(body))
        { parts.Add("--data-raw"); parts.Add(body); }
        parts.Add(url);

        var cmd = string.Join(" ", parts.Select(ShellQuote));
        // HttpRequest is sandbox-agnostic in semantics; dispatch via default repo
        // (repo=null never errors, so the runner is always present).
        var (httpRunner, _) = Resolve(repo: null);
        return httpRunner!.RunAsync(cmd, timeoutSeconds: clampedTimeout + 5, ct);
    }

    private static string ShellQuote(string s) => "'" + s.Replace("'", "'\\''") + "'";

    [Description("Searches a single file for lines matching a regular expression. Use when you already know the file path. context_before / context_after / context (shorthand) include adjacent lines. output_mode: 'content' (default, with line numbers), 'files_with_matches' (just the path), 'count' (matches per file).")]
    public Task<string> GrepInFile(
        [Description("Repository-relative path to a specific file.")] string path,
        [Description("Regular expression pattern to search for.")] string pattern,
        [Description("Maximum number of matches to return (default 1000).")] int? head_limit = null,
        [Description("Lines of context before each match (-B). Overridden by 'context' if set.")] int? context_before = null,
        [Description("Lines of context after each match (-A). Overridden by 'context' if set.")] int? context_after = null,
        [Description("Shorthand for context_before + context_after.")] int? context = null,
        [Description("'content' (default), 'files_with_matches', or 'count'.")] string output_mode = "content",
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: GrepInFile path={Path} pattern={Pattern}", path, pattern);
        if (_guards.CheckRead(path) is { } error) return Task.FromResult(error);
        var (before, after) = ResolveContext(context_before, context_after, context);
        var mode = ParseOutputMode(output_mode);
        var (runner, bare, routeErr) = Route(path);
        if (routeErr is not null) return Task.FromResult(routeErr);
        return runner!.GrepAsync(pattern, bare, glob: null, head_limit, before, after, mode, ct);
    }

    [Description("Searches all files under a directory tree for lines matching a regular expression. Use a glob filter (e.g. '*.cs') to narrow file types. context_before / context_after / context include adjacent lines. output_mode: 'content' (default), 'files_with_matches', or 'count'.")]
    public Task<string> GrepInTree(
        [Description("Regular expression pattern to search for.")] string pattern,
        [Description("Repository-relative directory to search under (default '.').")] string root = ".",
        [Description("Optional glob filter for file names (e.g. '*.cs', '**/*.json').")] string? glob = null,
        [Description("Maximum number of matches to return (default 1000).")] int? head_limit = null,
        [Description("Lines of context before each match (-B). Overridden by 'context' if set.")] int? context_before = null,
        [Description("Lines of context after each match (-A). Overridden by 'context' if set.")] int? context_after = null,
        [Description("Shorthand for context_before + context_after.")] int? context = null,
        [Description("'content' (default), 'files_with_matches', or 'count'.")] string output_mode = "content",
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: GrepInTree pattern={Pattern} root={Root} glob={Glob}", pattern, root, glob);
        if (_guards.CheckRead(root) is { } error) return Task.FromResult(error);
        var (before, after) = ResolveContext(context_before, context_after, context);
        var mode = ParseOutputMode(output_mode);
        var (runner, bareRoot, routeErr) = Route(root);
        if (routeErr is not null) return Task.FromResult(routeErr);
        return runner!.GrepAsync(pattern, bareRoot, glob, head_limit, before, after, mode, ct);
    }

    private static (int? Before, int? After) ResolveContext(int? before, int? after, int? context)
    {
        if (context is { } c && c > 0) return (c, c);
        return (before, after);
    }

    private static GrepOutputMode ParseOutputMode(string mode) => mode?.ToLowerInvariant() switch
    {
        "files_with_matches" => GrepOutputMode.FilesWithMatches,
        "count" => GrepOutputMode.Count,
        _ => GrepOutputMode.Content
    };

    [Description("Runs a shell command (passed to /bin/sh -c) in the named repo's sandbox. On multi-repo projects, the `repo` argument is REQUIRED — pick the repo whose toolchain matches what the command needs (e.g. repo='server' for `dotnet test`, repo='client' for `npm test`). On single-repo projects, `repo` is optional and defaults to the one repo. Returns labeled sections: header lines (exit_code, elapsed_ms, truncated) followed by 'stdout:' and 'stderr:' blocks. Destructive commands (rm/rmdir/unlink/shred/truncate/dd, raw device writes, fork bombs) are blocked by a defense-in-depth guard; use find/grep/head/wc/curl/ls/cat freely.")]
    public Task<string> RunCommand(
        [Description("Shell command to execute (passed to /bin/sh -c).")] string command,
        [Description("Repo whose sandbox runs the command. Required on multi-repo projects; optional on single-repo (defaults to the one repo).")] string? repo = null,
        [Description("Optional timeout in seconds (default 60, max 600). Use for builds and test runs that may exceed the default.")] int? timeout_seconds = null,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("tool_call: RunCommand cmd={Command} repo={Repo} timeout={Timeout}",
            command, repo, timeout_seconds);
        if (CommandGuard.Check(command) is { } error)
        {
            _logger?.LogWarning("RunCommand blocked by destructive-command guard: {Command}", command);
            return Task.FromResult(error);
        }
        if (_runners.Count > 1 && string.IsNullOrEmpty(repo))
            return Task.FromResult(
                $"Error: `repo` is required on multi-repo projects. " +
                $"Known repos: [{KnownRepoList()}].");
        var (cmdRunner, resolveErr) = Resolve(repo);
        if (resolveErr is not null) return Task.FromResult(resolveErr);
        return cmdRunner!.RunAsync(command, timeout_seconds, ct);
    }

    private static AIFunction Tool(Delegate impl, string name) =>
        AIFunctionFactory.Create(impl, name: name);

    // p0154: deprecated grep / glob / list_files aliases removed alongside the
    // agent-smith-skills 2.6.0 rename. The new tool surface is the only surface.
    private IEnumerable<AIFunction> ReadOnlySet() =>
    [
        Tool(ReadFile, "read_file"),
        Tool(GrepInFile, "grep_in_file"),
        Tool(GrepInTree, "grep_in_tree"),
        Tool(FindFiles, "find_files"),
        Tool(ListDirectory, "list_directory"),
        Tool(DirectoryTree, "directory_tree"),
        Tool(RunCommand, "run_command"),
        Tool(HttpRequest, "http_request"),
    ];

    private IEnumerable<AIFunction> InvestigatorSet() => ReadOnlySet();

    private IEnumerable<AIFunction> BootstrapSet() =>
    [
        Tool(ReadFile, "read_file"),
        Tool(WriteFile, "write_file"),
        Tool(Edit, "edit"),
        Tool(MultiEdit, "multi_edit"),
        Tool(GrepInFile, "grep_in_file"),
        Tool(GrepInTree, "grep_in_tree"),
        Tool(FindFiles, "find_files"),
        Tool(ListDirectory, "list_directory"),
        Tool(DirectoryTree, "directory_tree"),
        Tool(RunCommand, "run_command"),
        Tool(HttpRequest, "http_request"),
    ];

    // p0161d: BootstrapDiscover gets read-only filesystem only. No write_file,
    // no edit / multi_edit, no run_command, no http_request — Discover is a
    // read pass that produces the component list; writes happen in the
    // subsequent BootstrapRound rounds. ask_human is composed at the
    // AgenticToolSurface layer (HumanToolHost), not here.
    private IEnumerable<AIFunction> BootstrapDiscoverSet() =>
    [
        Tool(ReadFile, "read_file"),
        Tool(GrepInFile, "grep_in_file"),
        Tool(GrepInTree, "grep_in_tree"),
        Tool(FindFiles, "find_files"),
        Tool(ListDirectory, "list_directory"),
        Tool(DirectoryTree, "directory_tree"),
    ];

    private IEnumerable<AIFunction> All() => BootstrapSet();
}
