using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services.Structure;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-11-10-4778: a design turn's work clone of one repository — a fresh sandbox on the repository's root-first
/// toolchain image with build sizing and the project's secrets, like a coding run, mounting no shared package cache,
/// carrying the run id and no conversation id. Its ONE credentialed step is the clone, before any model command;
/// the detached checkout of the commit the turn's reads show carries no credential. A commit the clone does not
/// hold fails closed, so the model never runs on a different commit than it reads. The turn's scope observer is
/// told the clone is opening and then ready (or failed), as for a read scope.
/// </summary>
public sealed class WorkCloneOpener(
    ISandboxFactory factory, SandboxSpecBuilder specs, ISandboxLanguageResolver languages,
    IGitTokenResolver credentials, IRunContextAccessor runContext, ISourceScopeObserverAccessor? observers = null)
{
    public const int DefaultDepth = 50;

    /// <summary>The name the turn's activity shows the clone under.</summary>
    public static string ProgressName(string repo) => $"{repo} work clone";

    public async Task<ISandbox> OpenAsync(
        ResolvedProject project, RepoConnection repo, string sha, bool fullHistory, CancellationToken ct)
    {
        await ReportAsync(repo, SourceScopeProgress.Opening, ct);
        try
        {
            var opened = await CloneAsync(project, repo, sha, fullHistory, ct);
            await ReportAsync(repo, SourceScopeProgress.Ready, ct);
            return opened;
        }
        catch
        {
            await ReportAsync(repo, SourceScopeProgress.Failed, CancellationToken.None);
            throw;
        }
    }

    private Task ReportAsync(RepoConnection repo, SourceScopeProgress progress, CancellationToken ct) =>
        observers?.Current?.ReportAsync(ProgressName(repo.Name), progress, ct) ?? Task.CompletedTask;

    private async Task<ISandbox> CloneAsync(
        ResolvedProject project, RepoConnection repo, string sha, bool fullHistory, CancellationToken ct)
    {
        var spec = await SpecAsync(project, repo, ct);
        var sandbox = await factory.CreateAsync(spec, ct);
        try
        {
            var clone = await sandbox.RunStepAsync(CloneStep(repo, credentials.For(repo), fullHistory), null, ct);
            if (clone.ExitCode != 0) throw new InvalidOperationException($"git clone of '{repo.Name}' failed: {Text(clone)}");
            var checkout = await sandbox.RunStepAsync(CheckoutStep(sha), null, ct);
            if (checkout.ExitCode != 0)
                throw new InvalidOperationException(
                    $"The commit your reads of '{repo.Name}' show ({sha}) is not in this clone"
                    + (fullHistory ? string.Empty : $" of the last {DefaultDepth} commits — ask for history: full")
                    + $"; nothing runs on a different commit. {Text(checkout)}");
            return sandbox;
        }
        catch
        {
            await sandbox.DisposeAsync();
            throw;
        }
    }

    /// <summary>The root-first discovered toolchain through the chain (project overrides first), build-sized.</summary>
    public async Task<SandboxSpec> SpecAsync(ResolvedProject project, RepoConnection repo, CancellationToken ct)
    {
        var discoveries = await languages.ResolveAllAsync(repo, ContextReadBranches.None, ct);
        var root = discoveries.FirstOrDefault(d => StructureWorkdir.Normalize(d.Workdir) == string.Empty)
                   ?? discoveries.FirstOrDefault();
        return specs.Build(project, root?.Language, PipelinePresets.CodeName, root?.ToolchainImage, root?.Resources) with
        {
            RunId = runContext.CurrentRunId, ConversationId = null, NoPackageCaches = true, RequiresPerJobBus = true,
        };
    }

    public static Step CloneStep(RepoConnection repo, GitCredential credential, bool fullHistory) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git",
            Args: ["-c", GitStepCredentials.Helper, "clone",
                .. fullHistory ? new[] { "--single-branch" } : ["--depth", DefaultDepth.ToString(System.Globalization.CultureInfo.InvariantCulture), "--single-branch"],
                repo.Url!, "."],
            WorkingDirectory: Repository.SandboxWorkPath,
            Env: CloneEnv(credential),
            TimeoutSeconds: 300);

    // LFS objects are not fetched: the smudge filter would be a second credentialed download inside the clone step.
    private static IReadOnlyDictionary<string, string> CloneEnv(GitCredential credential) =>
        new Dictionary<string, string>(GitStepCredentials.TokenEnv(credential) ?? new Dictionary<string, string>())
        {
            ["GIT_LFS_SKIP_SMUDGE"] = "1",
        };

    public static Step CheckoutStep(string sha) =>
        new(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "git", Args: ["checkout", "--detach", sha],
            WorkingDirectory: Repository.SandboxWorkPath, TimeoutSeconds: 60);

    private static string Text(StepResult result) =>
        (result.ErrorMessage ?? result.OutputContent ?? string.Empty).Trim();
}
