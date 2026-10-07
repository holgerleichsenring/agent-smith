using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-06-03c7d: a real git repository behind an in-process sandbox, on the ticket
/// branch, with one unrelated file committed — what the series layout tests write into.</summary>
internal sealed class SeriesGitRepo : IAsyncDisposable
{
    internal const string Branch = "agent-smith/4";
    private readonly InProcessSandbox _sandbox;
    private readonly string _seriesBase;

    private SeriesGitRepo(InProcessSandbox sandbox, string seriesBase, string sandboxKey)
    {
        _sandbox = sandbox;
        _seriesBase = seriesBase;
        Files = new SandboxFileReaderFactory();
        Ops = SeriesDoubles.Git(Files);
        Pipeline = new PipelineContext();
        Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { [sandboxKey] = sandbox });
        Pipeline.Set(ContextKeys.Repository, new Repository(new BranchName(Branch), "/work"));
    }

    internal SandboxFileReaderFactory Files { get; }
    internal SandboxGitOperations Ops { get; }
    internal PipelineContext Pipeline { get; }
    internal ISandbox Sandbox => _sandbox;
    internal string WorkDir => _sandbox.WorkDir;

    internal static async Task<SeriesGitRepo?> CreateAsync(
        string seedPath, string seriesBase, string sandboxKey = "primary")
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return null;
        var dir = Path.Combine(Path.GetTempPath(), "as-03c7-git-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var repo = new SeriesGitRepo(
            new InProcessSandbox("test", dir, ownsWorkDir: true, NullLogger.Instance), seriesBase, sandboxKey);
        await repo.GitAsync("init", "-q", "-b", Branch);
        await repo.GitAsync("config", "user.email", "test@example.test");
        await repo.GitAsync("config", "user.name", "test");
        await repo.CommitFilesAsync(seedPath);
        return repo;
    }

    internal async Task CommitFilesAsync(params string[] paths)
    {
        foreach (var path in paths)
            await Files.Create(_sandbox).WriteAsync(path, Edited(path), default);
        await GitAsync(["add", "-f", "--", .. paths]);
        await GitAsync("commit", "-q", "-m", "edit");
    }

    // A spec file is edited as a reviewer would — still a spec, with a changed goal.
    private string Edited(string path)
    {
        var name = SeriesPaths.FileName(path);
        return name.EndsWith(".yaml", StringComparison.Ordinal) && name.StartsWith(_seriesBase, StringComparison.Ordinal)
            ? $"spec: {name[..(_seriesBase.Length + 1)]}\ngoal: \"Edited {Guid.NewGuid():N}\"\ndone:\n  - \"Done.\"\n"
            : $"edited {Guid.NewGuid():N}\n";
    }

    internal async Task<IReadOnlyList<string>> TrackedAsync() =>
        (await GitAsync("ls-files", ".agentsmith"))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    internal async Task<string> HeadAsync() => (await GitAsync("rev-parse", "HEAD")).Trim();

    internal async Task<string> GitAsync(params string[] args)
    {
        var result = await _sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: args, TimeoutSeconds: 30), null, CancellationToken.None);
        result.ExitCode.Should().Be(0, result.ErrorMessage);
        return result.OutputContent ?? string.Empty;
    }

    public ValueTask DisposeAsync() => _sandbox.DisposeAsync();
}
