using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// 2026-08-31-f634: per configured repository and context, whether a path from an edit to
/// a verdict is DECLARED — the <c>verify:</c> stages of that context's own context.yaml,
/// read where that repository lives, without starting a run.
/// <para>
/// This check REPORTS; it never refuses anything. It belongs to the doctor family, whose
/// members are read at startup and on health, and it has to reach the network — the run
/// family, which can fail a run, may not. The refusal already lives where the data does: a
/// run that resolves no verification command fails at the point it would otherwise have
/// been called a success.
/// </para>
/// <para>
/// A repository that declares nothing is NOT a finding. That is what an estate looks like
/// before anyone onboards it, and the report says so in those words. The one finding here
/// is a repository that could not be READ — because "declare your stages" is the wrong
/// instruction to hand an operator whose credential is what actually failed.
/// </para>
/// </summary>
public sealed class VerificationOnboardingCheck(
    IPreflightConfigSource configSource,
    ISandboxLanguageResolver contextResolver) : IPreflightCheck
{
    private const string Cost = "one context.yaml read per context, no sandbox";

    public string Name => "verify-onboarding";

    public string Category => "repo";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var config = configSource.Resolve().Config;
        if (config is null)
            return PreflightCheckResult.Skip("agentsmith.yml failed to load — see config-schema");
        if (config.Repos.Count == 0)
            return PreflightCheckResult.Skip($"no repository configured ({Cost})");

        var declared = new List<string>();
        var unreadable = new List<string>();
        foreach (var (name, repo) in config.Repos)
            await InspectAsync(name, repo, declared, unreadable, cancellationToken);

        return unreadable.Count > 0
            ? PreflightCheckResult.Fail(
                Join(unreadable.Concat(declared)),
                "The configured credential could not list '.agentsmith/contexts' in that repository. "
                + "Fix the repo's auth secret (token/SSH key) and url — until it can be read, nothing "
                + "is known about what it declares, and a missing declaration is not the diagnosis.")
            : PreflightCheckResult.Pass(Join(declared));
    }

    private async Task InspectAsync(
        string name, RepoConnection repo, List<string> declared, List<string> unreadable,
        CancellationToken cancellationToken)
    {
        // 2026-09-01-1335: a repository located by path is read from its working copy, so
        // only one that names nowhere at all stays uninspected.
        if (!repo.HasLocation)
        {
            declared.Add($"{name}: not inspected (no url and no path — nowhere to read)");
            return;
        }

        var listing = await contextResolver.ListContextsAsync(repo, cancellationToken);
        if (listing.UnreadableReason is { } reason)
        {
            unreadable.Add($"{name}: contexts unreadable — {reason}");
            return;
        }

        if (listing.Contexts.Count == 0)
        {
            declared.Add($"{name}: not yet declared (no readable context)");
            return;
        }

        foreach (var context in listing.Contexts)
            declared.Add(Describe(name, context));
    }

    // "not yet declared" is the starting state of every repository, so it is written as a
    // state and never as a complaint — the operator is being told where the tool can work,
    // not being marked down for an estate they have not onboarded yet.
    private static string Describe(string repo, RemoteContextDiscovery context) =>
        context.Verify is { Count: > 0 } stages
            ? $"{repo}/{context.ContextName}: eligible — verify declares "
                + $"[{string.Join(", ", stages.Select(s => s.Label))}]"
            : $"{repo}/{context.ContextName}: not yet declared";

    private static string Join(IEnumerable<string> lines) =>
        $"{string.Join(" | ", lines)} ({Cost})";
}
