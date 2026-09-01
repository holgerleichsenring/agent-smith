using AgentSmith.Contracts.Models;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: the small declarations and findings the api mechanics cases score, so
/// each case states the ONE thing it is about and nothing else.
/// </summary>
internal static class ApiCorpusFixtures
{
    internal const string WeakEndpoint = "/members/{id}";
    internal const string SoundEndpoint = "/orders/{id}";

    internal static ApiTargetDeclaration OneOfEach() => new()
    {
        Id = "mechanics",
        Endpoints =
        [
            new ApiEndpointDeclaration
            {
                Method = "GET", Path = WeakEndpoint,
                Verdict = ApiTargetDeclaration.Verdicts.Weak, Class = "missing-authorization",
            },
            new ApiEndpointDeclaration
            {
                Method = "GET", Path = SoundEndpoint,
                Verdict = ApiTargetDeclaration.Verdicts.Sound, Class = "unscoped-identifier",
            },
        ],
    };

    /// <summary>An api finding as the delivery layer carries one: no file at all, the
    /// endpoint in ApiPath.</summary>
    internal static SkillObservation FindingOn(string apiPath) => new(
        Id: 1, Role: "api-security-master", Concern: ObservationConcern.Security,
        Description: $"weakness at {apiPath}", Suggestion: "fix it", Blocking: false,
        Severity: ObservationSeverity.High, Confidence: 80, ApiPath: apiPath);

    internal static ApiCorpusReport ReportOf(
        ApiTargetDeclaration declaration, params SkillObservation[] findings) =>
        new("test-model", "0000abcd", DateTimeOffset.UnixEpoch, declaration.Id,
            ApiCorpusScoring.Score(declaration, findings), [], null)
        {
            UndeclaredLocations = ApiCorpusScoring.UndeclaredLocations(declaration, findings),
        };
}
