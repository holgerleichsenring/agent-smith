using System.Text.Json.Nodes;
using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-02-3f06b: every fact of a PLANNED spec cites evidence that resolves against this
/// tree — each path a file, each line inside it — or is a dated observation.
/// <para>
/// PLANNED ONLY. An active spec changes the files its facts describe, so judging it would red its
/// own run; done/ rots by design, as the code moves on under a finished record. A PR that moves a
/// file a planned fact cites re-grounds that fact in the same PR — the failure names the spec,
/// the claim and the path so whoever moved it can.
/// </para>
/// </summary>
public sealed class SpecEvidenceRuleTests
{
    private readonly EvidenceCheck _check = new(new EvidenceReferences());

    private readonly FileSystemEvidenceProbe _probe = new(ArchitectureSources.RepositoryRoot);

    [Fact]
    public async Task SpecEvidence_EveryPlannedFact_Resolves()
    {
        var failures = new List<string>();
        foreach (var file in SpecYamlFile.All().Where(f => f.State == "planned"))
            foreach (var (claim, evidence) in Facts(file))
                failures.AddRange((await _check.CheckAsync(
                        evidence, EvidencePolicy.Repository, _probe, CancellationToken.None))
                    .Select(problem => $"{file.SpecId}: \"{claim}\" — {problem}"));

        failures.Should().BeEmpty(
            "a planned fact cites what resolves, or says it is a dated 'observed:'. If your change "
            + "moved a file a plan cites, re-ground that fact here.\n  " + string.Join("\n  ", failures));
    }

    private static IEnumerable<(string Claim, string Evidence)> Facts(SpecYamlFile file) =>
        (file.Document?["facts"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(fact => (Text(fact["claim"]), Text(fact["evidence"])));

    private static string Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : node?.ToJsonString() ?? string.Empty;
}
