using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-19-6b0ba: a series of several specs for a harness run, which no model cuts any more. It rides in as the
/// approved record a filed ticket carries, so DeriveSpec hands it to the branch once and the sequence runs every member
/// in order — what a run needs to watch more than one spec block. A ticket without one gets a stand-in per repository.
/// </summary>
internal static class HarnessSeries
{
    public const string Base = "2026-11-19-0b0b";

    private const string Platform = "harness";

    /// <summary>A spec of the series: its goal, its one done line, the repository it changes and its one step.</summary>
    public sealed record Member(string Goal, string Done, string Repo = "primary", string Step = "guard");

    public static readonly Member[] TwoSpecs =
    [
        new("Introduce the empty-payload guard", "An empty request body is answered with 400."),
        new("Move the existing callers onto the guard", "No caller builds its own empty-payload check.", Step: "callers"),
    ];

    /// <summary>Carries the series into <paramref name="runner"/>'s run as the stub ticket's approved record.</summary>
    public static PipelineRunner WithSeries(this PipelineRunner runner, params Member[] members)
    {
        // The key follows the tracker platform, so the record names the run's ticket whichever stub provider serves it.
        runner.RequestContext[ContextKeys.TrackerPlatform] = Platform;
        var key = TicketKey.For(Platform, "1").Value;
        var specs = members.Select((m, i) => Spec(SeriesIdFactory(i), m)).ToList();
        var set = new SpecSet(key, specs, SpecAccounting.Empty, [], SpecSource.Approved,
            Approval: new SpecApproval(DateTimeOffset.UtcNow.AddMinutes(-5), "harness", "sample.approver"), Series: Base);
        // The approval names the run's repositories, so it narrows nothing the run would otherwise hold.
        IReadOnlyList<string> repositories = runner.ReposOverride is { Count: > 0 } run
            ? [.. run.Select(r => r.Name)] : [.. members.Select(m => m.Repo).Distinct()];
        var record = new SpecApprovalRecord(key, set, repositories);
        runner.RequestContext[ContextKeys.ApprovedSpecSet] = SpecApprovalJson.Write(record);
        return runner;
    }

    private static string SeriesIdFactory(int index) => $"{Base}{(char)('a' + index)}";

    private static DerivedSpec Spec(string id, Member member)
    {
        var yaml = $"spec: {id}\ngoal: \"{member.Goal}\"\nrepos: [{member.Repo}]\nsteps:\n  - id: {member.Step}\n"
            + $"    action: \"{member.Goal}\"\ndone:\n  - \"{member.Done}\"\n";
        return new DerivedSpec(new SpecDraftReader().Read(yaml) with { Repos = [member.Repo] }, id, string.Empty, []);
    }
}
