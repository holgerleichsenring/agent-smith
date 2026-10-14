using System.Globalization;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762n: what git says about one citation — the commit that wrote it into the spec's own file,
/// the tree it was written against, its old text, where its text is now. Each question is one Run step in
/// the run's full clone; a step that fails, times out or throws answers null: NOT CHECKED, never a finding.
/// </summary>
public sealed class SpecCitationHistory(ILogger<SpecCitationHistory> logger)
{
    private const int TimeoutSeconds = 120;
    private const string DefaultBranch = "origin/HEAD";
    // POSIX ERE after the escaped token: not a digit, - or , — or a , before a non-digit or the line end.
    private const string RightBound = "([^0-9,-]|,[^0-9]|,$|$)";

    /// <summary>The newest commit that changed how often <paramref name="token"/> occurs in the spec's own
    /// file — bounded on the right, so a longer token never re-dates it — asked of each repository in turn.</summary>
    public async Task<SpecCitationWrite?> WriteAsync(
        IReadOnlyDictionary<string, ISandbox> repositories, string specId, string token, CancellationToken ct)
    {
        foreach (var (name, sandbox) in repositories)
        {
            var found = await GitAsync(sandbox, ["log", "-1", "--format=%H %ct", "--pickaxe-regex",
                $"-S{Escape(token)}{RightBound}", "--", .. SpecFile(specId)], ct);
            if (found?.Split(' ') is not [var sha, var seconds] || !long.TryParse(seconds, out var epoch)) continue;
            var added = await GitAsync(sandbox, ["log", "-1", "--diff-filter=A", "--format=%H", "--", .. SpecFile(specId)], ct);
            return new SpecCitationWrite(name, sha, DateTimeOffset.FromUnixTimeSeconds(epoch), added == sha);
        }
        return null;
    }

    /// <summary>The tree the citation was written against in one repository: the writing commit where it was
    /// written, else that repository's first-parent HEAD at its date — or, for an approved set's filed text,
    /// the first-parent default branch at the approval instant.</summary>
    public Task<string?> BaseAsync(
        ISandbox sandbox, string repository, SpecCitationWrite write, DateTimeOffset? approvedAt, CancellationToken ct) =>
        approvedAt is { } at ? BeforeAsync(sandbox, at, DefaultBranch, ct)
        : repository == write.Repository ? Task.FromResult<string?>(write.Sha)
        : BeforeAsync(sandbox, write.Date, "HEAD", ct);

    public async Task<bool> ExistsAsync(ISandbox sandbox, string at, string path, CancellationToken ct) =>
        await GitAsync(sandbox, ["cat-file", "-e", $"{at}:{path}"], ct) is not null;

    public async Task<bool> UnchangedAsync(ISandbox sandbox, string at, string path, CancellationToken ct) =>
        await GitAsync(sandbox, ["diff", "--quiet", at, "--", path], ct) is not null;

    public Task<string?> OldTextAsync(ISandbox sandbox, string at, string path, CancellationToken ct) =>
        GitAsync(sandbox, ["show", $"{at}:{path}"], ct, trim: false);

    /// <summary>The tracked files holding <paramref name="line"/> verbatim; empty when none does.</summary>
    public async Task<IReadOnlyList<string>?> CandidatesAsync(ISandbox sandbox, string line, CancellationToken ct)
    {
        var result = await StepAsync(sandbox, ["grep", "-l", "-F", "-e", line], ct);
        if (result is not { ExitCode: 0 or 1, TimedOut: false }) return null;
        return result.ExitCode == 1 ? [] : (result.OutputContent ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>The file as it stands in the working tree, read whole (1 MB); null when it cannot be read.</summary>
    public async Task<string?> CurrentTextAsync(ISandbox sandbox, string path, CancellationToken ct)
    {
        if (!ContainedPath.TryRelative(path, out var relative)) return null;
        var result = await RunAsync(sandbox, new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile,
            TimeoutSeconds: TimeoutSeconds, Path: ContainedPath.Absolute(relative), WorkingDirectory: Repository.SandboxWorkPath), ct);
        return result is { ExitCode: 0, TimedOut: false } ? result.OutputContent ?? string.Empty : null;
    }

    private async Task<string?> BeforeAsync(ISandbox sandbox, DateTimeOffset instant, string rev, CancellationToken ct) =>
        await GitAsync(sandbox, ["rev-list", "-1", "--first-parent",
            "--before=" + instant.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss +0000", CultureInfo.InvariantCulture), rev], ct)
            is { Length: > 0 } sha ? sha : null;

    private static string[] SpecFile(string specId) =>
        [$"{SeriesPaths.SpecsRoot}/*/{specId}-*", $"{SeriesPaths.SpecsRoot}/*/{specId}.*"];

    internal static string Escape(string token) => Regex.Replace(token, @"[\\.\[\](){}*+?|^$]", @"\$0");

    private async Task<string?> GitAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct, bool trim = true)
    {
        var result = await StepAsync(sandbox, args, ct);
        if (result is not { ExitCode: 0, TimedOut: false }) return null;
        return trim ? (result.OutputContent ?? string.Empty).Trim() : result.OutputContent ?? string.Empty;
    }

    private Task<StepResult?> StepAsync(ISandbox sandbox, IReadOnlyList<string> args, CancellationToken ct) =>
        RunAsync(sandbox, new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git", Args: args,
            WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: TimeoutSeconds), ct);

    // On the run's token: a sandbox that went silent is a question that could not be asked.
    private async Task<StepResult?> RunAsync(ISandbox sandbox, Step step, CancellationToken ct)
    {
        try { return await sandbox.RunStepAsync(step, progress: null, ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Citation history step {Command} {Args} could not run", step.Command, step.Args);
            return null;
        }
    }
}

/// <summary>2026-10-13-8762n: the commit that wrote a citation into its spec's file, in the repository that
/// carries the file; <see cref="IsAdded"/> when that commit is the one that added the file.</summary>
public sealed record SpecCitationWrite(string Repository, string Sha, DateTimeOffset Date, bool IsAdded);
