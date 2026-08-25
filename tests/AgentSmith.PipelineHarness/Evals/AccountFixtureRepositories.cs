using System.Diagnostics;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: turns a fixture's two trees into real git repositories the account can
/// be pointed at.
/// <para>
/// Real, not simulated, because the account SEARCHES. A fake sandbox answering a fixed set
/// of patterns would measure whether the account asks the questions the fixture author
/// expected; a real tree answers whatever it asks, which is the only way the score means
/// anything after the prompt changes.
/// </para>
/// <para>
/// The base is written as a commit AND as <c>refs/remotes/origin/&lt;base&gt;</c> with
/// <c>origin/HEAD</c> pointing at it, because that is where the production code looks for a
/// base — the delivery diff asks the clone, and a fixture that answered differently would
/// take its diff against something no run ever compares against.
/// </para>
/// <para>
/// Each fixture is BUILT once per test run and COPIED thereafter, the same trade
/// <see cref="AgentSmith.Tests"/>' migrated-store template makes for its schema: building a
/// repository costs eight git subprocesses, copying one costs a directory walk. That lesson
/// was learned the expensive way — on a two-core runner a burst of process spawns starves
/// async continuations, and the first casualty is whichever test elsewhere asserts a
/// wall-clock bound. This suite is not entitled to spend that on nine identical rebuilds.
/// </para>
/// </summary>
public sealed class AccountFixtureRepositories : IAsyncDisposable
{
    public const string BaseBranch = "main";
    public const string WorkBranch = "work";

    // Built once per fixture id, copied per use. Guarded because xUnit runs classes in
    // parallel and two builders of one template would race on the same directory.
    private static readonly Lock TemplateGate = new();
    private static readonly Dictionary<string, string> Templates = new(StringComparer.Ordinal);

    private readonly List<ISandbox> _sandboxes = [];
    private readonly string _root;

    private AccountFixtureRepositories(string root) => _root = root;

    public IReadOnlyDictionary<string, ISandbox> Sandboxes { get; private set; }
        = new Dictionary<string, ISandbox>();

    public static async Task<AccountFixtureRepositories> MaterialiseAsync(
        AccountFixture fixture, ILoggerFactory loggerFactory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var template = await TemplateAsync(fixture, ct);
        var root = Path.Combine(Path.GetTempPath(), "account-eval-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        var built = new AccountFixtureRepositories(root);
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal);

        foreach (var repo in fixture.Repositories)
        {
            var path = Path.Combine(root, repo.Name);
            CopyTree(Path.Combine(template, repo.Name), path);
            var sandbox = new InProcessSandbox(
                $"{fixture.Id}:{repo.Name}", path, ownsWorkDir: false,
                loggerFactory.CreateLogger<InProcessSandbox>());
            built._sandboxes.Add(sandbox);
            sandboxes[repo.Name] = sandbox;
        }

        built.Sandboxes = sandboxes;
        return built;
    }

    /// <summary>
    /// The built repositories for this fixture, made once. The template is never written to —
    /// every caller works on its own copy, because a copy is not a share.
    /// </summary>
    private static async Task<string> TemplateAsync(AccountFixture fixture, CancellationToken ct)
    {
        lock (TemplateGate)
        {
            if (Templates.TryGetValue(fixture.Id, out var existing)) return existing;
        }

        var root = Path.Combine(
            Path.GetTempPath(), "account-eval-template-" + Guid.NewGuid().ToString("n"));
        foreach (var repo in fixture.Repositories)
        {
            var path = Path.Combine(root, repo.Name);
            Directory.CreateDirectory(path);
            await BuildAsync(path, repo, ct);
        }

        lock (TemplateGate)
        {
            // Another class may have built it while this one was working; theirs is as good,
            // and keeping one means the loser's tree is simply never read again.
            if (Templates.TryGetValue(fixture.Id, out var raced)) return raced;
            Templates[fixture.Id] = root;
            return root;
        }
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(to, Path.GetRelativePath(from, file)), overwrite: true);
    }

    private static async Task BuildAsync(
        string path, AccountFixtureRepo repo, CancellationToken ct)
    {
        await GitAsync(path, ct, "init", "--quiet", "--initial-branch", BaseBranch);
        await GitAsync(path, ct, "config", "user.email", "fixture@example.com");
        await GitAsync(path, ct, "config", "user.name", "Account Fixture");

        Write(path, repo.Base);
        await GitAsync(path, ct, "add", "-A");
        await GitAsync(path, ct, "commit", "--quiet", "-m", "base");

        // The clone's own answer to "what do I merge into" is a remote-tracking ref, so the
        // fixture creates one rather than leaving the production resolver to fall through.
        await GitAsync(path, ct, "update-ref", $"refs/remotes/origin/{BaseBranch}", "HEAD");
        await GitAsync(path, ct, "symbolic-ref", "refs/remotes/origin/HEAD",
            $"refs/remotes/origin/{BaseBranch}");

        await GitAsync(path, ct, "checkout", "--quiet", "-b", WorkBranch);
        foreach (var relative in repo.Base.Keys.Where(k => !repo.Branch.ContainsKey(k)))
            File.Delete(Path.Combine(path, relative.Replace('/', Path.DirectorySeparatorChar)));
        Write(path, repo.Branch);
        await GitAsync(path, ct, "add", "-A");
        await GitAsync(path, ct, "commit", "--quiet", "-m", "delivery");
    }

    private static void Write(string root, IReadOnlyDictionary<string, string> tree)
    {
        foreach (var (relative, content) in tree)
        {
            var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }

    /// <summary>A failed setup command is thrown, never warned: a fixture built wrong would
    /// score the account against a tree nobody designed.</summary>
    private static async Task GitAsync(string workDir, CancellationToken ct, params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = workDir,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("git could not be started");
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {string.Join(' ', args)} failed in {workDir}: {stderr}");
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sandbox in _sandboxes) await sandbox.DisposeAsync();
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp tree is not worth failing a measurement over.
        }
    }
}
