using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762k: two members of one series, their contract, and the series reviewer as the tests
/// compose them.</summary>
internal static class SeriesReviewFixtures
{
    public const string A = "2026-10-14-1234a";
    public const string B = "2026-10-14-1234b";
    public const string AQuote = "The export writes every order";
    public const string BGoal = "The import reads every invoice of the week into one table.";
    public const string SpecFile = ".agentsmith/specs/active/2026-10-14-1234a-export.yaml";

    public static SpecDraft DraftA() => new(A, "The export writes every order of the day into one file.", AttackReviewFixtures.Yaml, []);

    public static SpecDraft DraftB() => new(B, BGoal, AttackReviewFixtures.Yaml
        .Replace($"spec: {A}", $"spec: {B}", StringComparison.Ordinal)
        .Replace("The export writes every order of the day into one file.", BGoal, StringComparison.Ordinal), []);

    public static SeriesContract Contract(string owner = A) => new(
        [new SeriesContractName("OrderExported", "event", 87, owner)],
        [new SeriesContractDatum("export file", "the export folder", owner)], [], []);

    public static SeriesContractInput Input(bool inRun = true) =>
        new(Contract(), [new(DraftA(), inRun ? SeriesContractMember.Cleared : null),
            new(DraftB(), inRun ? SeriesContractMember.Cleared : null)], inRun)
        {
            RepositoryNames = AttackReviewFixtures.Names,
        };

    public static string Answer(string verdict, string? cuts = null, string? findings = null) =>
        $$"""Checked, citing [A1]: {"verdict": "{{verdict}}", "cuts": [{{cuts}}], "findings": [{{findings}}]}""";

    public static string Cut(string? specId, string register, string quote, string cites, int line = 3) =>
        $$"""{"spec_id": {{(specId is null ? "null" : $"\"{specId}\"")}}, "register": "{{register}}", "quote": "{{quote}}", "replacement": "The export writes nothing", "repository": "{{AttackReviewFixtures.Key}}", "path": "{{SpecFile}}", "line": {{line}}, "cites": "{{cites}}"}""";

    public static SeriesContractReviewer Reviewer(IChatClientFactory factory) => new(factory, new AsyncLocalRunContextAccessor(),
        new SeriesContractAdmission(new SpecAttackAdmission(NullLogger<SpecAttackAdmission>.Instance),
            NullLogger<SeriesContractAdmission>.Instance),
        NullLogger<SeriesContractReviewer>.Instance);
}
