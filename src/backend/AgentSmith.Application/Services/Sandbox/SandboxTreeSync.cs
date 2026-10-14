using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Levels every sandbox of one repository to the union of their work before a stage runs.
/// <para>
/// A repository whose contexts need two toolchain images is two independent clones, and the
/// master writes through one of them; a stage in the other would build a tree that never
/// saw the work. Hub and spoke through the primary — the sandbox every commit path already
/// treats as the repository: each secondary's change is folded into it, the union is
/// committed through the checkpoint's secret-scanned path, and only then spread back. A path
/// two sandboxes changed to different bytes is red and nothing is applied anywhere. A
/// repository with one sandbox runs no git step.
/// </para>
/// </summary>
public sealed class SandboxTreeSync(
    SandboxTargets targets, SandboxTreeGit git, SandboxTreeSpread spread,
    SandboxGitOperations gitOps, RunWorkCheckpointer checkpointer, ILogger<SandboxTreeSync> logger)
{
    public async Task<SandboxTreeSyncResult> SyncAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) || repos is null)
            return SandboxTreeSyncResult.Level;
        try
        {
            var plans = new List<SandboxTreeSpread.Plan>();
            foreach (var repo in repos)
                if (targets.SandboxesForRepo(pipeline, repo) is { Count: > 1 } matches)
                    plans.Add(await PlanAsync(repo.Name, matches, ct));
            if (plans.Count == 0) return SandboxTreeSyncResult.Level;
            foreach (var plan in plans)
                foreach (var patch in plan.Fold)
                    if (await git.ApplyAsync(plan.Primary.Value, patch.Encoded, checkOnly: false, ct) is { } failed)
                        throw new InvalidOperationException($"{patch.Sender}: its change did not apply to {plan.Primary.Key}: {failed}");
            if (plans.Any(p => p.NeedsPush))
            {
                await checkpointer.PushNowAsync(pipeline, ct);
                foreach (var plan in plans.Where(p => p.NeedsPush))
                    if (await gitOps.HasWorkingChangesAsync(plan.Primary.Value, ct))
                        return SandboxTreeSyncResult.Red(
                            $"Sandbox tree sync: {plan.Repository}: the union could not be committed "
                            + $"(secret scan or push declined) in {plan.Primary.Key}");
            }
            foreach (var plan in plans)
                await spread.SpreadAsync(plan, ct);
            logger.LogInformation("Sandbox tree sync: {Count} repository(ies) level", plans.Count);
            return SandboxTreeSyncResult.Level;
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Sandbox tree sync is red: {Reason}", ex.Message);
            return SandboxTreeSyncResult.Red($"Sandbox tree sync: {ex.Message}");
        }
    }

    /// <summary>Everything read before anything is applied: trees, base, own changes, conflicts,
    /// every encoded change, and every fold checked against the primary.</summary>
    private async Task<SandboxTreeSpread.Plan> PlanAsync(
        string repository, IReadOnlyList<KeyValuePair<string, ISandbox>> sandboxes, CancellationToken ct)
    {
        var trees = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, sandbox) in sandboxes)
            trees[key] = await git.StageAndWriteTreeAsync(key, sandbox, ct);
        var primary = sandboxes[0];
        if (trees.Values.All(tree => tree == trees[primary.Key]))
            return new SandboxTreeSpread.Plan(repository, sandboxes, await git.IndexDiffersFromHeadAsync(primary.Value, ct), [], []);

        var secondaries = sandboxes.Skip(1).ToList();
        var bases = new List<string>();
        foreach (var (key, sandbox) in secondaries)
            bases.Add(await git.HeadTreeAsync(key, sandbox, ct));
        if (bases.Distinct(StringComparer.Ordinal).Count() > 1)
            throw new InvalidOperationException(
                $"{repository}: the sandboxes {string.Join(", ", secondaries.Select(s => s.Key))} stand on different base trees");
        var baseTree = bases[0];
        if (!await git.HasObjectAsync(primary.Value, baseTree, ct))
            throw new InvalidOperationException($"{secondaries[0].Key}: its base tree {baseTree} is unknown to {primary.Key}");

        var own = new List<(string Key, IReadOnlyDictionary<string, string> Own)>();
        foreach (var (key, sandbox) in sandboxes)
            own.Add((key, await git.PostImagesAsync(key, sandbox, baseTree, trees[key], ct)));
        var conflicts = SandboxTreeConflicts.Of(own);
        if (conflicts.Describe() is { } conflict) throw new InvalidOperationException(conflict);

        async Task<SandboxTreeSpread.Patch?> PatchAsync(KeyValuePair<string, ISandbox> sender, string receiver) =>
            await git.EncodedDiffAsync(sender.Key, sender.Value, baseTree, trees[sender.Key],
                    conflicts.Excluded(receiver, sender.Key), ct) switch
            {
                null => throw new InvalidOperationException(
                    $"{sender.Key}: its change reached the {SandboxTreeGit.EncodedLimit}-character limit of an encoded delta; nothing was applied"),
                "" => null,
                var encoded => new SandboxTreeSpread.Patch(sender.Key, receiver, encoded),
            };

        var fold = new List<SandboxTreeSpread.Patch>();
        var spreads = new List<SandboxTreeSpread.Patch>();
        foreach (var secondary in secondaries)
        {
            if (await PatchAsync(secondary, primary.Key) is { } folded) fold.Add(folded);
            foreach (var sender in sandboxes.Where(s => s.Key != secondary.Key))
                if (await PatchAsync(sender, secondary.Key) is { } spreading) spreads.Add(spreading);
        }
        foreach (var patch in fold)
            if (await git.ApplyAsync(primary.Value, patch.Encoded, checkOnly: true, ct) is { } failed)
                throw new InvalidOperationException(
                    $"{patch.Sender}: its change does not apply to {primary.Key}, so nothing was applied: {failed}");
        return new SandboxTreeSpread.Plan(repository, sandboxes, NeedsPush: true, fold, spreads);
    }
}
