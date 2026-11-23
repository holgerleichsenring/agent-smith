using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-13-5cdf: one answer to "which epic record is this run a slice of?", read from
/// the ticket's own labels.
/// <para>
/// 2026-09-13-a72a stamped the parent on an epic CHILD as a reserved-prefix label, because
/// a label is the only place both the polling and the webhook path can read it without a
/// tracker round-trip. The base ladder and the work branch must name the same parent, so
/// they read it here rather than each reaching into the label list.
/// </para>
/// <para>
/// 2026-09-17-0e79d: the framework stamps no filed ticket any more — an epic's work ticket cuts
/// from its own base and its slice records carry no stamp at all — so this answers null for
/// everything it files, and names a parent only for a legacy child or a hand-stamped ticket.
/// </para>
/// <para>
/// 2026-11-17-9a39b: a STACKED run answers its unlanded prerequisite first — the claim stamped it, and its pushed
/// branch is this run's rung. It wins over a label parent: the prerequisite's branch was cut from that rung already.
/// </para>
/// <para>
/// 2026-11-23-31dca: the stamp is per repo, so every answer is for ONE repo: its stacked prerequisite, else the label
/// parent. A repo the stamp has no entry for cuts from the base it would have unstacked.
/// </para>
/// </summary>
public static class RunParentTicket
{
    /// <summary>The ticket whose branch this repo of the run is cut from, or null when it cuts from its own base.</summary>
    public static string? ForRepo(PipelineContext pipeline, string? repo) =>
        Stacked(pipeline)?.For(repo) ?? LabelParent(pipeline);

    /// <summary>Whether this repo's answer is a prerequisite's branch — a rung nobody may publish, only find.</summary>
    public static bool IsStackedIn(PipelineContext pipeline, string? repo) => Stacked(pipeline)?.For(repo) is not null;

    /// <summary>The repository a sandbox of the run serves, from the coordinator's key -> repo map; the run's only repo when
    /// there is no map; null when neither names one.</summary>
    public static string? RepoOfSandbox(PipelineContext pipeline, string sandboxKey)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var owners) && owners is not null
            && owners.TryGetValue(sandboxKey, out var owner)) return owner;
        return pipeline.TryGet<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, out var repos) && repos is { Count: 1 }
            ? repos[0].Name : null;
    }

    /// <summary>The epic parent the ticket's labels name, the same for every repo; null for everything filed now.</summary>
    public static string? LabelParent(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null
            ? FiledTicketLabels.ParentId(ticket.Labels)
            : null;
    }

    /// <summary>The claim's stamp — a repo map, in process or as a JSON object after a round-trip, or the old single id
    /// (a string or JSON string) for every repo; null when the run is not stacked.</summary>
    public static StackedBases? Stacked(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<object>(ContextKeys.StackedOnTicket, out var value)) return null;
        var bases = value switch
        {
            string text => Every(text),
            JsonElement { ValueKind: JsonValueKind.String } element => Every(element.GetString()),
            JsonElement { ValueKind: JsonValueKind.Object } element => PerRepo(element.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String).Select(p => KeyValuePair.Create(p.Name, p.Value.GetString()))),
            IReadOnlyDictionary<string, string> map => PerRepo(map.Select(p => KeyValuePair.Create(p.Key, (string?)p.Value))),
            _ => null,
        };
        return bases is { PerRepo.Count: 0, EveryRepo: null } ? null : bases;
    }

    private static StackedBases Every(string? id) =>
        new(new Dictionary<string, string>(), string.IsNullOrWhiteSpace(id) ? null : id.Trim());

    private static StackedBases PerRepo(IEnumerable<KeyValuePair<string, string?>> entries) =>
        new(entries.Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .ToDictionary(p => p.Key, p => p.Value!.Trim(), StringComparer.Ordinal));
}
