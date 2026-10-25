using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>2026-10-25-9dbbf: every node of a pull request's review, all pages read; each thread with all its comments.</summary>
public sealed record GitHubPrReviewNodes(
    string? Viewer,
    IReadOnlyList<(JsonElement Thread, IReadOnlyList<JsonElement> Comments)> Threads,
    IReadOnlyList<JsonElement> Reviews,
    IReadOnlyList<JsonElement> Comments);
