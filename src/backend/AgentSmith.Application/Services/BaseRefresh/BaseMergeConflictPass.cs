using AgentSmith.Application.Services.Handlers;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-9dc0a: the base-merge master's work on the paths the unions left unmerged, judged by the server. The master's
/// whole=false stops at once; otherwise <see cref="BaseMergeWholeness"/> decides — nothing open is whole, whether conclude
/// was called or the loop ended on its cap or in prose. A pass that left paths open but made progress is continued in
/// the same conversation while the cost budget lasts; one that made none is asked once, with conclude as its only tool,
/// for the reason a person reads. Progress only falls, so no count of its own bounds it.
/// </summary>
public sealed class BaseMergeConflictPass(IBaseMergeMaster master, BaseMergeWholeness wholeness)
{
    public async Task<BaseMergeResolution> ResolveAsync(BaseMergeSituation situation, IReadOnlyList<string> conflicted, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(situation);
        var before = await wholeness.CheckAsync(situation.Sandbox, conflicted, [], ct, stage: false);
        var pass = await master.RunAsync(situation with { Unmerged = conflicted }, ct);
        while (true)
        {
            var open = await wholeness.CheckAsync(situation.Sandbox, conflicted, Wrote(pass), ct);
            // whole=false stops even over a tree the check finds whole: the master said its sides contradict.
            if (pass.Whole == false) return new BaseMergeResolution(open.IsWhole ? conflicted : open.PathNames, pass.Reason, Wrote(pass));
            if (open.IsWhole) return new BaseMergeResolution([], pass.Reason, Wrote(pass));
            if (open.IsProgressOver(before) && !PipelineCostTracker.GetOrCreate(situation.Pipeline).IsBudgetExhausted)
            {
                before = open;
                pass = await master.ContinueAsync(pass, $"The server's check finds these still open: {open.Say()}. Go on.", false, ct);
                continue;
            }
            var asked = await master.ContinueAsync(pass, $"The server's check finds these still open: {open.Say()}. "
                + "Call conclude with whole=false and the reason a person needs to take it from here.", true, ct);
            return new BaseMergeResolution(open.PathNames, asked.Reason ?? NoWord, Wrote(pass));
        }
    }

    private const string NoWord = "the master ended without saying why";

    private static IReadOnlyList<string> Wrote(BaseMergePass pass) =>
        [.. pass.Wrote.Select(w => w.Path.Value).Distinct(StringComparer.Ordinal)];
}
