using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Integration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-12-f5f7f: two real git repositories — Server and Client — each with a bare origin, checked
/// out on the ticket branch in one run: what a multi-repository spec write pushes into, and where a
/// hook makes one push fail.
/// </summary>
internal sealed class TwoRepoSeries : IAsyncDisposable
{
    internal const string Branch = "agent-smith/4711";
    internal static readonly RepoConnection Server = new() { Name = "Server", Type = RepoType.Local };
    internal static readonly RepoConnection Client = new() { Name = "Client", Type = RepoType.Local };

    private readonly Dictionary<string, (InProcessSandbox Sandbox, string Origin)> _repos = new(StringComparer.Ordinal);

    internal SandboxFileReaderFactory Files { get; } = new();

    internal ISandbox Sandbox(string repo) => _repos[repo].Sandbox;

    /// <summary>Both repositories, or null where git is not installed.</summary>
    internal static async Task<TwoRepoSeries?> CreateAsync()
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return null;
        var series = new TwoRepoSeries();
        foreach (var name in new[] { Server.Name, Client.Name })
            await series.AddAsync(name);
        return series;
    }

    /// <summary>A fresh run over both repositories, on the ticket branch.</summary>
    internal PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            _repos.ToDictionary(r => r.Key, r => (ISandbox)r.Value.Sandbox));
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, _repos.Keys.ToDictionary(k => k, k => k));
        pipeline.Set(ContextKeys.Repository, new Repository(new BranchName(Branch), "/work"));
        return pipeline;
    }

    /// <summary>Installs a hook — in the origin (<c>pre-receive</c>, <c>post-receive</c>) or the checkout.</summary>
    internal void Hook(string repo, string name, string body, bool inOrigin = true)
    {
        var dir = inOrigin ? Path.Combine(_repos[repo].Origin, "hooks") : Path.Combine(_repos[repo].Sandbox.WorkDir, ".git", "hooks");
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>The sha the origin's ticket branch points at.</summary>
    internal async Task<string> RemoteHeadAsync(string repo) =>
        (await GitAsync(repo, "--git-dir", _repos[repo].Origin, "rev-parse", Branch)).Trim();

    internal async Task<string> HeadAsync(string repo) => (await GitAsync(repo, "rev-parse", "HEAD")).Trim();

    internal async Task<string> TreeAsync(string repo, string sha) => (await GitAsync(repo, "rev-parse", $"{sha}^{{tree}}")).Trim();

    /// <summary>What the next run's checkout sees: the checkout reset to what the origin holds.</summary>
    internal async Task CheckOutRemoteAsync(string repo)
    {
        await GitAsync(repo, "fetch", "-q", "origin", Branch);
        await GitAsync(repo, "reset", "-q", "--hard", "FETCH_HEAD");
    }

    internal async Task<string> GitAsync(string repo, params string[] args)
    {
        var result = await _repos[repo].Sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(),
            StepKind.Run, Command: "git", Args: args, TimeoutSeconds: 30), null, CancellationToken.None);
        result.ExitCode.Should().Be(0, result.ErrorMessage);
        return result.OutputContent ?? string.Empty;
    }

    private async Task AddAsync(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "as-f5f7f-" + Guid.NewGuid().ToString("N"));
        var work = Path.Combine(root, "work");
        Directory.CreateDirectory(work);
        _repos[name] = (new InProcessSandbox("test", work, ownsWorkDir: true, InProcessStepEnvironment.Inherit, NullLogger.Instance), Path.Combine(root, "origin.git"));
        await GitAsync(name, "init", "-q", "--bare", _repos[name].Origin);
        await GitAsync(name, "init", "-q", "-b", Branch);
        await GitAsync(name, "config", "user.email", "test@example.test");
        await GitAsync(name, "config", "user.name", "test");
        await Files.Create(Sandbox(name)).WriteAsync("README.md", $"{name}\n", default);
        await GitAsync(name, "add", "README.md");
        await GitAsync(name, "commit", "-q", "-m", "seed");
        await GitAsync(name, "remote", "add", "origin", _repos[name].Origin);
        await GitAsync(name, "push", "-q", "origin", Branch);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (sandbox, origin) in _repos.Values)
        {
            await sandbox.DisposeAsync();
            try { Directory.Delete(Path.GetDirectoryName(origin)!, recursive: true); } catch (IOException) { }
        }
    }
}
