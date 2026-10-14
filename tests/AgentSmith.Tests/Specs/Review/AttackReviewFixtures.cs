using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762e: an attack review's spec, look and minted evidence as the tests compose them.</summary>
internal static class AttackReviewFixtures
{
    public const string Key = "shop-dotnet";
    public const string Repository = "shop";

    public const string Yaml = """
        spec: 2026-10-14-1234a
        goal: "The export writes every order of the day into one file."
        scope:
          in: "The export handler and its file writer."
          out: "The import side stays as it is."
        decisions:
          - key: "ONE FILE PER DAY keeps the archive readable by hand."
        steps:
          - id: writer
            action: "Write the orders through the buffered file writer."
        tests:
          - "Export_WritesEveryOrderOfTheDay"
        done:
          - "Every order of the day lands in the export file."
        facts:
          - claim: "The export handler reads the orders from the order store."
            evidence: "src/Export/ExportHandler.cs:12"
        """;

    public static SpecDraft Draft(params string[] requires) =>
        new("2026-10-14-1234a", "The export writes every order of the day into one file.", Yaml, requires)
        {
            Facts = [new SpecFact("The export handler reads the orders from the order store.", "src/Export/ExportHandler.cs:12")],
        };

    public static IReadOnlyDictionary<string, string> Names { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) { [Key] = Repository };

    public static DerivationLook Look() =>
        new(new Dictionary<string, ISandbox> { [Key] = new CountingSandbox(0) },
            new FixedReaderFactory(new InMemorySandboxFileReader()), new PackageEcosystemDetector(),
            NullLogger.Instance, terms: DerivationLookTerms.AttackReview);

    public static string Read(DerivationLook look, string path, int lines, int exit = 0) =>
        look.Evidence.Remember(new EvidenceRecord(Key, EvidenceRecord.Read, $"read {path}", exit, Ran: true, path, lines));

    public static string Search(DerivationLook look, string pattern, int exit) =>
        look.Evidence.Remember(new EvidenceRecord(
            Key, EvidenceRecord.Search, $"grep -E '{pattern}' .", exit, Ran: exit is 0 or 1, "."));

    public static SpecAttackResult Admit(string json, DerivationLook look) =>
        new SpecAttackAdmission(NullLogger<SpecAttackAdmission>.Instance).Admit(
            SpecAttackAnswerReader.Read(json)!, SpecRegisters.Read(Yaml), look, Names);

    public static SpecAttackInput Input(SpecDraft? draft = null) =>
        new(draft ?? Draft(), "spec", 1, 0, "Make the nightly export usable by accounting.", true, [], 0, 1, []);
}
