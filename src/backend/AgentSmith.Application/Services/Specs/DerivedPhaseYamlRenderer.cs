using YamlDotNet.Serialization;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: renders one derived phase as a schema-valid phase spec. The YAML is
/// composed in CODE from the model's fields rather than written by the model:
/// a phase spec that must pass SpecDraftValidator is a contract, and a contract
/// the model formats is a contract that fails on a stray indent.
/// </summary>
public sealed class DerivedPhaseYamlRenderer
{
    private readonly ISerializer _serializer = new SerializerBuilder().Build();

    public string Render(
        string phaseId,
        string goal,
        IReadOnlyList<string> requires,
        IReadOnlyList<(string Id, string Action)> steps,
        IReadOnlyList<string> done,
        string markdownFileName,
        IReadOnlyList<int> carriedSegments,
        string ticketId,
        PhaseFacts? facts = null)
    {
        var document = new Dictionary<string, object?>
        {
            ["phase"] = phaseId,
            ["goal"] = goal,
        };
        if (requires.Count > 0) document["requires"] = requires;
        document["scope"] = new Dictionary<string, object?>
        {
            ["in"] = carriedSegments.Count > 0
                ? $"Ticket {ticketId}, segment(s) {string.Join(", ", carriedSegments)} — "
                  + $"carried verbatim in {markdownFileName}"
                : $"Ticket {ticketId}",
        };
        document["decisions"] = new[]
        {
            new Dictionary<string, object?>
            {
                // The companion is named IN the spec: a phase whose constraints live in
                // a file nobody is told to open is a phase that drops them.
                ["key"] =
                    $"Derived from ticket {ticketId} by agent-smith. The verbatim naming rules, "
                    + $"forbidden APIs and code templates this phase must honour are carried "
                    + $"byte-identical in {markdownFileName} — read it, never a summary of it.",
            },
        };
        if (steps.Count > 0)
            document["steps"] = steps
                .Select(s => new Dictionary<string, object?> { ["id"] = s.Id, ["action"] = s.Action })
                .ToList();
        document["done"] = done;
        // 2026-09-07-b7e2: two keys of the phase's own, admitted by the schema's open top
        // level. Each fact carries the minted evidence line it cites, so the id a human
        // reads resolves to the look it names. Absent when there is nothing to write, so
        // a cut with no facts renders exactly as it did before this phase.
        if (facts is { Facts.Count: > 0 })
            document["facts"] = facts.Facts
                .Select(f => new Dictionary<string, object?> { ["claim"] = f.Claim, ["evidence"] = f.Evidence })
                .ToList();
        if (facts is { Assumptions.Count: > 0 })
            document["assumptions"] = facts.Assumptions;

        return _serializer.Serialize(document).TrimEnd() + "\n";
    }
}
