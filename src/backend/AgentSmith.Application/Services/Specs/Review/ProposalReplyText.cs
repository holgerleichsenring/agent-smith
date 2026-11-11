using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using YamlDotNet.Serialization;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: the reply a design turn answers with when the proposal it publishes is not exactly what
/// its last reply proposed — a draft reverted, an early end. The first proposal reply's prose is kept and its
/// draft block replaced by one rendered from the published proposal: a spec's ```yaml block, or the epic's
/// ```outcome block with parent and children set to the published drafts. On Slack and Teams the reply is
/// where a draft is read, and the next turn replays it, so it must be what will be filed. 2026-11-11-2bdfb: when the
/// published children are another id set than the first reply's, the contract block is the published contract.
/// </summary>
public static class ProposalReplyText
{
    /// <param name="lastReply">2026-11-09-0620m: the re-draft, whose block stands for a kind this class does not render.</param>
    public static string Render(string firstReply, OutcomeProposal published, string? lastReply = null)
    {
        ArgumentNullException.ThrowIfNull(firstReply);
        ArgumentNullException.ThrowIfNull(published);
        var block = published switch
        {
            SpecOutcome spec => Fence("yaml", spec.Draft.Yaml),
            EpicOutcome epic => Fence("outcome", EpicBody(firstReply, epic)),
            _ => SpecDialogDraftBlocks.OutcomeBlocks(lastReply) is { Count: > 0 } blocks ? blocks[0].Value : null,
        };
        return block is null ? firstReply : TurnReply.WithBlock(firstReply, block);
    }

    private static string EpicBody(string reply, EpicOutcome epic)
    {
        var blocks = SpecDialogDraftBlocks.OutcomeBlocks(reply);
        var map = (blocks.Count > 0 ? Read(blocks[0].Groups[1].Value) : null) as Dictionary<object, object?>
            ?? new Dictionary<object, object?> { ["kind"] = "epic" };
        map["parent"] = Read(epic.Parent.Yaml);
        if (!SameIds(map, epic)) Contract(map, epic.Contract);
        map["children"] = epic.Children.Select(child => Read(child.Yaml)).ToList();
        return OutcomeYamlReader.ToYaml(map);
    }

    // 2026-11-11-2bdfb: a re-cut set publishes the reply's contract, so the text carries the published one.
    private static bool SameIds(Dictionary<object, object?> map, EpicOutcome epic) =>
        map.TryGetValue("children", out var node) && node is List<object?> children
        && children.Select(c => (c as Dictionary<object, object?>)?.GetValueOrDefault("spec")?.ToString())
            .ToHashSet(StringComparer.Ordinal).SetEquals(epic.Children.Select(c => c.SpecId));

    private static void Contract(Dictionary<object, object?> map, Contracts.Specs.SeriesContract? contract)
    {
        if (Specs.SeriesContractYaml.Write(contract) is not { } document)
        {
            map.Remove(Specs.SeriesContractReader.Key);
            return;
        }
        map[Specs.SeriesContractReader.Key] = Read(new SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull).Build().Serialize(document));
    }

    private static object? Read(string yaml)
    {
        try
        {
            return new DeserializerBuilder().Build().Deserialize<object?>(yaml);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }

    private static string Fence(string language, string body) => $"```{language}\n{body.TrimEnd()}\n```";
}
