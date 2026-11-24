using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: scans a revision and records what the rules decided. A blocked verdict stores the verdict and the
/// agent's explanation, blocks a PoC that never reached ready and ends the run blocked, nothing committed. An overrule
/// run passes only when every block is a vulnerability whose id the overrules lifted under 'code'; a new or secret block
/// stores the new verdict and stays blocked. A passing revision is validated (or overruled), and a blocked PoC goes back
/// to importing.
/// </summary>
public sealed class PocScanGate(
    PocCodeScanner scanner, PocScanExplanation explanation, IPocScanStore store, PocOverruledPublisher overruled,
    PocsConfigSource config)
{
    public async Task<CommandResult> RunAsync(PocCodeScanContext context, PocScanSubject subject, CancellationToken ct)
    {
        var scan = config.Current().Scan;
        var (verdict, failure) = await scanner.ScanAsync(context.Project, subject, scan, ct);
        if (verdict is null) return CommandResult.Fail($"The code scan of revision {subject.Revision} of '{subject.PocName}' could not run: {failure}");
        var lifted = PocOverruledIds.FromJson(subject.OverruledIdsJson).Of(PocScanStages.Code);
        if (Passes(verdict, lifted, context.Seeds.Overrule)) return await PassAsync(context, subject, verdict, ct);
        var (text, why) = await explanation.ExplainAsync(subject.PocName, verdict, context.Project.Agent,
            PipelineCostTracker.GetOrCreate(context.Pipeline), ct);
        await store.RecordVerdictAsync(new PocScanOutcome(subject.PocId, subject.Revision, PocRevisionStates.Blocked, verdict.ToJson(), text), ct);
        return CommandResult.Fail($"Revision {subject.Revision} of '{subject.PocName}' is blocked by {verdict.Blocks.Count} "
            + $"finding(s); nothing was committed.{(why is null ? string.Empty : $" No explanation was written: {why}")}");
    }

    private static bool Passes(PocScanVerdict verdict, IReadOnlyList<string> lifted, bool overrule) =>
        !verdict.IsBlocked
        || (overrule && verdict.Blocks.All(b => b.Kind == PocScanFindingKinds.Vulnerability && lifted.Contains(b.Id, StringComparer.Ordinal)));

    private async Task<CommandResult> PassAsync(PocCodeScanContext context, PocScanSubject subject, PocScanVerdict verdict, CancellationToken ct)
    {
        var state = verdict.IsBlocked ? PocRevisionStates.Overruled : PocRevisionStates.Validated;
        await store.RecordVerdictAsync(new PocScanOutcome(subject.PocId, subject.Revision, state, verdict.ToJson(), null), ct);
        if (!verdict.IsBlocked) return CommandResult.Ok($"Revision {subject.Revision} of '{subject.PocName}' passed the code scan ({verdict.Notes.Count} note(s)).");
        var seeds = context.Seeds;
        await overruled.PublishAsync(context.Pipeline, subject.PocId, subject.Revision, PocScanStages.Code, seeds.Actor ?? string.Empty,
            seeds.OverruleReason ?? string.Empty, [.. verdict.Blocks.Select(b => b.Id)], ct);
        return CommandResult.Ok($"Revision {subject.Revision} of '{subject.PocName}' was overruled: {verdict.Blocks.Count} vulnerability block(s) lifted.");
    }
}
