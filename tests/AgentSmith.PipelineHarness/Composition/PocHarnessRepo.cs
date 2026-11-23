using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-11-23-933dc: what a poc-deploy run needs beyond 933db's intake in the real composition — a source repo (a local
/// bare repository on main, the PoC fixture's poc-src pointing at it), real in-process sandboxes for the commit step,
/// the secret patterns (copies of the catalog's), and the queue recorded. Null without git.
/// </summary>
internal sealed class PocHarnessRepo : IAsyncDisposable
{
    private const string Patterns = """
        name: secrets
        patterns:
          - id: aws-access-key
            regex: "AKIA[0-9A-Z]{16}"
            severity: critical
            confidence: 9
            title: "AWS Access Key ID"
            description: "Hardcoded AWS access key identifier"
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "as-933dc-compose-" + Guid.NewGuid().ToString("N"));

    internal string Origin => Path.Combine(_root, "origin.git");

    internal List<PipelineRequest> Queued { get; } = [];

    /// <summary>The source repo as the run's project names it (the harness runner builds its own project).</summary>
    internal Contracts.Models.Configuration.RepoConnection Repo => new()
    {
        Name = "poc-src", Type = Contracts.Models.Configuration.RepoType.Local, Url = Origin, DefaultBranch = "main",
    };

    internal static async Task<PocHarnessRepo?> CreateAsync()
    {
        var repo = new PocHarnessRepo();
        var seed = Path.Combine(repo._root, "seed");
        Directory.CreateDirectory(seed);
        if ((await repo.RunAsync(seed, ["--version"])).ExitCode != 0) return null;
        await File.WriteAllTextAsync(Path.Combine(seed, "README.md"), "pocs\n");
        await repo.RunAsync(seed, ["init", "-q", "-b", "main"]);
        await repo.RunAsync(seed, ["-c", "user.email=t@example.test", "-c", "user.name=t", "add", "-A"]);
        await repo.RunAsync(seed, ["-c", "user.email=t@example.test", "-c", "user.name=t", "commit", "-q", "-m", "seed"]);
        await repo.RunAsync(repo._root, ["clone", "-q", "--bare", seed, repo.Origin]);
        return repo;
    }

    /// <summary>The PoC fixture with poc-src pointing at the bare origin.</summary>
    internal string Fixture()
    {
        var text = File.ReadAllText(FixturePaths.For(FixturePaths.Pocs))
            .Replace("url: https://stub.test/poc-src", $"url: {Origin}\n    default_branch: main", StringComparison.Ordinal);
        var path = FixturePaths.For($".poc-repo-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, text);
        return path;
    }

    internal void Override(IServiceCollection services)
    {
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => Queued.Add(r)).Returns(Task.CompletedTask);
        services.RemoveAll<IRedisJobQueue>().AddSingleton(queue.Object);
        services.RemoveAll<ISandboxFactory>().AddSingleton<ISandboxFactory>(new GitSandboxes(_root));
        Directory.CreateDirectory(Path.Combine(_root, "catalog", "patterns"));
        File.WriteAllText(Path.Combine(_root, "catalog", "patterns", "secrets.yaml"), Patterns);
        services.RemoveAll<ISkillsCatalogPath>().AddSingleton<ISkillsCatalogPath>(
            Mock.Of<ISkillsCatalogPath>(c => c.Root == Path.Combine(_root, "catalog") && c.Origin == "test"));
    }

    /// <summary>git's output in <see cref="Origin"/>.</summary>
    internal async Task<string> OriginAsync(params string[] args) =>
        ((await RunAsync(_root, ["--git-dir", Origin, .. args])).OutputContent ?? string.Empty).Trim();

    private async Task<StepResult> RunAsync(string dir, string[] args)
    {
        Directory.CreateDirectory(dir);
        await using var sandbox = new InProcessSandbox("git", dir, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        return await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run, Command: "git",
            Args: ["-C", dir, .. args], TimeoutSeconds: 60), null, CancellationToken.None);
    }

    public ValueTask DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        return ValueTask.CompletedTask;
    }

    /// <summary>Real in-process sandboxes, one fresh directory each — what the commit step clones into.</summary>
    private sealed class GitSandboxes(string root) : ISandboxFactory
    {
        public Task<ISandbox> CreateAsync(SandboxSpec spec, CancellationToken cancellationToken)
        {
            var dir = Path.Combine(root, "sandbox-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            return Task.FromResult<ISandbox>(new InProcessSandbox("poc", dir, ownsWorkDir: true, InProcessStepEnvironment.Inherit, NullLogger.Instance));
        }
    }
}
