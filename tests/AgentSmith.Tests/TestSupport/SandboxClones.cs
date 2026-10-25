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
/// One repository served by several toolchain sandboxes: independent real-git clones of one
/// local bare origin, each behind an in-process sandbox, on the run branch — or, as under
/// --source-path, several sandboxes over ONE working directory.
/// </summary>
internal sealed class SandboxClones : IAsyncDisposable
{
    internal const string Repo = "shop";
    internal const string Branch = "agent-smith/7";
    internal static readonly string[] Keys = ["shop-dotnet", "shop-node", "shop-python"];
    internal static readonly byte[] Binary = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF, 0x0D];

    private readonly string _root;
    private readonly Dictionary<string, (ISandbox Sandbox, string Dir)> _clones = new(StringComparer.Ordinal);

    private SandboxClones(string root)
    {
        _root = root;
        Pipeline = new PipelineContext();
    }

    internal PipelineContext Pipeline { get; }
    internal string Primary => Keys[0];
    internal string Secondary => Keys[1];
    internal IReadOnlyDictionary<string, ISandbox> Sandboxes =>
        _clones.ToDictionary(c => c.Key, c => c.Value.Sandbox, StringComparer.Ordinal);
    internal IReadOnlyList<KeyValuePair<string, ISandbox>> Matches => [.. Sandboxes];

    /// <param name="wrap">Puts a fault-injecting decorator in front of a sandbox, by key.</param>
    internal static async Task<SandboxClones?> CreateAsync(
        int count = 2, bool sharedDirectory = false, Func<string, ISandbox, ISandbox>? wrap = null)
    {
        if (!SandboxToolAvailability.IsAvailable("git")) return null;
        var root = Path.Combine(Path.GetTempPath(), "as-8762a-" + Guid.NewGuid().ToString("N"));
        var seed = Path.Combine(root, "seed");
        Directory.CreateDirectory(Path.Combine(seed, "src", "Api"));
        Directory.CreateDirectory(Path.Combine(seed, "web"));
        Directory.CreateDirectory(Path.Combine(seed, "docs"));
        await File.WriteAllTextAsync(Path.Combine(seed, "src", "Api", "Program.cs"), "class Program {}\n");
        await File.WriteAllTextAsync(Path.Combine(seed, "web", "app.ts"), "export const app = 1;\n");
        await File.WriteAllTextAsync(Path.Combine(seed, "docs", "crlf.txt"), "one\r\ntwo\r\n");
        var clones = new SandboxClones(root);
        await GitIn(root, seed, "init", "-q", "-b", Branch);
        await Configure(root, seed);
        await GitIn(root, seed, "add", "-A");
        await GitIn(root, seed, "commit", "-q", "-m", "seed");
        await GitIn(root, root, "clone", "-q", "--bare", seed, Path.Combine(root, "origin.git"));
        for (var i = 0; i < count; i++)
        {
            var dir = Path.Combine(root, sharedDirectory ? "shared" : Keys[i]);
            if (!sharedDirectory || i == 0)
            {
                await GitIn(root, root, "clone", "-q", "-b", Branch, Path.Combine(root, "origin.git"), dir);
                await Configure(root, dir);
            }
            ISandbox sandbox = new InProcessSandbox(Keys[i], dir, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
            clones._clones[Keys[i]] = (wrap?.Invoke(Keys[i], sandbox) ?? sandbox, dir);
        }
        clones.Seat();
        return clones;
    }

    private void Seat()
    {
        Pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, Sandboxes);
        Pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos,
            _clones.Keys.ToDictionary(k => k, _ => Repo, StringComparer.Ordinal));
        Pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos,
            [new RepoConnection { Name = Repo, Type = RepoType.Local, Path = _root }]);
        Pipeline.Set(ContextKeys.Repository, new Repository(new BranchName(Branch), "/work"));
        Pipeline.Set(ContextKeys.RunId, "run-8762a");
    }

    internal string PathOf(string key, string path) => Path.Combine(_clones[key].Dir, path);

    internal Task WriteAsync(string key, string path, string text) =>
        WriteBytesAsync(key, path, System.Text.Encoding.UTF8.GetBytes(text));

    internal async Task WriteBytesAsync(string key, string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(key, path))!);
        await File.WriteAllBytesAsync(PathOf(key, path), bytes);
    }

    internal Task<byte[]> ReadBytesAsync(string key, string path) => File.ReadAllBytesAsync(PathOf(key, path));

    internal async Task<string> ReadAsync(string key, string path) => await File.ReadAllTextAsync(PathOf(key, path));

    internal Task<string> GitAsync(string key, params string[] args) => GitIn(_root, _clones[key].Dir, args);

    internal async Task CommitAllAsync(string key, string message)
    {
        await GitAsync(key, "add", "-A");
        await GitAsync(key, "commit", "-q", "-m", message);
    }

    internal async Task<string> HeadAsync(string key) => (await GitAsync(key, "rev-parse", "HEAD")).Trim();

    internal async Task<string> HeadTreeAsync(string key) => (await GitAsync(key, "rev-parse", "HEAD^{tree}")).Trim();

    internal async Task<string> StatusAsync(string key) => (await GitAsync(key, "status", "--porcelain")).Trim();

    private static async Task Configure(string root, string dir)
    {
        await GitIn(root, dir, "config", "user.email", "test@example.test");
        await GitIn(root, dir, "config", "user.name", "test");
    }

    private static async Task<string> GitIn(string root, string dir, params string[] args)
    {
        await using var sandbox = new InProcessSandbox("git", root, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
        var result = await sandbox.RunStepAsync(new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: ["-C", dir, .. args], TimeoutSeconds: 60), null, CancellationToken.None);
        result.ExitCode.Should().Be(0, $"git {string.Join(' ', args)}: {result.ErrorMessage}");
        return result.OutputContent ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (sandbox, _) in _clones.Values) await sandbox.DisposeAsync();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

/// <summary>A sandbox that answers chosen steps itself, or acts after one ran.</summary>
internal sealed class FaultingSandbox(
    ISandbox inner, Func<Step, StepResult?>? fault = null, Action<Step, StepResult>? after = null) : ISandbox
{
    public string JobId => inner.JobId;

    public async Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
    {
        if (fault?.Invoke(step) is { } faulted) return faulted;
        var result = await inner.RunStepAsync(step, progress, ct);
        after?.Invoke(step, result);
        return result;
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    internal static StepResult Failed(Step step, string error) =>
        new(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 1, TimedOut: false,
            DurationSeconds: 0.01, ErrorMessage: error, OutputContent: null);
}
