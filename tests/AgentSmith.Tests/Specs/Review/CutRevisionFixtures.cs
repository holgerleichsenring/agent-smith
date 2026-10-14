using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762f: a spec a round's cuts are applied to, and the cuts and problems the tests name.</summary>
internal static class CutRevisionFixtures
{
    public const string Yaml = """
        # yaml-language-server: $schema=../../spec.schema.json
        spec: 2026-10-14-5678a
        goal: "The export writes every order of the day into one file."
        applies_to: "backend (export)"
        requires: [2026-10-14-1234a]
        scope:
          in: "The export handler and its file writer. The writer flushes after every order."
          out: "The import side stays as it is."
        decisions:
          - key: "ONE FILE PER DAY keeps the archive readable by hand."
        steps:
          - id: writer
            action: "Write the orders through the buffered file writer."
            path: "shop:src/Export/Writer.cs"
          - id: wire
            action: "Register the writer in the export installer."
        tests:
          - "Export_WritesEveryOrderOfTheDay"
        done:
          - "Every order of the day lands in the export file."
          - "The writer flushes after every single order."
          - "A failed write is logged with the order id."
        facts:
          - claim: "The export handler reads the orders from the order store."
            evidence: "src/Export/ExportHandler.cs:12"
          - claim: "The order store pages its reads by one hundred."
            evidence: "src/Orders/OrderStore.cs:40"
        """;

    public static SpecReviewCut Cut(string register, string quote, string? replacement = null) =>
        new(register, quote, replacement, "shop/src/Export/Writer.cs:9 [A1]");

    public static SpecEvidenceProblem Problem(string claim = "The order store pages its reads by one hundred.") =>
        new(claim, "src/Orders/OrderStore.cs has 31 lines");

    public static SpecDraft Draft(string yaml = Yaml) => new SpecDraftReader().Read(yaml);

    /// <summary>The fixture with one exact text replaced — the test fails loudly when the text is absent.</summary>
    public static string With(string from, string to, string yaml = Yaml) =>
        yaml.Contains(from, StringComparison.Ordinal)
            ? yaml.Replace(from, to, StringComparison.Ordinal)
            : throw new ArgumentException($"the fixture has no '{from}'");

    public static string Reply(string yaml) => $"Here is the revised spec.\n\n```yaml\n{yaml}\n```\n";
}
