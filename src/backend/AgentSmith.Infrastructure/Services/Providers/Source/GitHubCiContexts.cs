using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Reviews;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: one statusCheckRollup context of a GitHub commit read as a check — a check run (from an app, GitHub
/// Actions among them) or a commit status — with what it says about a failure: title, summary, text and annotations,
/// or the status description, and its link.
/// </summary>
public static class GitHubCiContexts
{
    private static readonly string[] FailedConclusions = ["FAILURE", "TIMED_OUT", "CANCELLED", "ACTION_REQUIRED", "STARTUP_FAILURE"];

    public static CiCheck Check(JsonElement node) => IsCheckRun(node)
        ? new CiCheck(Str(node, "name") ?? "check", RunOutcome(node), Bool(node, "isRequired"), Time(node, "completedAt"))
        : new CiCheck(Str(node, "context") ?? "status", StatusOutcome(Str(node, "state")), Bool(node, "isRequired"), Time(node, "createdAt"));

    /// <summary>The host's own words about the failure, annotations as path:line message.</summary>
    public static IReadOnlyList<string?> Summary(JsonElement node) => IsCheckRun(node)
        ? [Str(node, "title"), Str(node, "summary"), Str(node, "text"), .. Annotations(node)]
        : [Str(node, "description")];

    public static string? Link(JsonElement node) => Str(node, IsCheckRun(node) ? "detailsUrl" : "targetUrl");

    /// <summary>The Actions job id of a check run GitHub Actions ran; null for any other context.</summary>
    public static long? ActionsJob(JsonElement node) =>
        IsCheckRun(node) && node.TryGetProperty("checkSuite", out var suite) && suite.ValueKind == JsonValueKind.Object
        && suite.TryGetProperty("app", out var app) && app.ValueKind == JsonValueKind.Object && Str(app, "slug") == "github-actions"
        && node.TryGetProperty("databaseId", out var id) && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : null;

    private static bool IsCheckRun(JsonElement node) => Str(node, "__typename") == "CheckRun";

    private static CiCheckOutcome RunOutcome(JsonElement node) =>
        Str(node, "status") != "COMPLETED" ? CiCheckOutcome.Pending
        : FailedConclusions.Contains(Str(node, "conclusion")) ? CiCheckOutcome.Failed : CiCheckOutcome.Passed;

    private static CiCheckOutcome StatusOutcome(string? state) => state switch
    {
        "SUCCESS" => CiCheckOutcome.Passed,
        "FAILURE" or "ERROR" => CiCheckOutcome.Failed,
        _ => CiCheckOutcome.Pending,
    };

    private static IEnumerable<string?> Annotations(JsonElement node) =>
        node.TryGetProperty("annotations", out var a) && a.ValueKind == JsonValueKind.Object
        && a.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array
            ? nodes.EnumerateArray().Select(Annotation) : [];

    private static string Annotation(JsonElement n)
    {
        var line = n.TryGetProperty("location", out var l) && l.ValueKind == JsonValueKind.Object
            && l.TryGetProperty("start", out var s) && s.ValueKind == JsonValueKind.Object
            && s.TryGetProperty("line", out var v) && v.ValueKind == JsonValueKind.Number ? $":{v.GetInt32()}" : string.Empty;
        return $"{Str(n, "path")}{line} {Str(n, "message")}".Trim();
    }

    private static bool Bool(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? Time(JsonElement e, string name) =>
        Str(e, name) is { } at ? DateTimeOffset.Parse(at, CultureInfo.InvariantCulture) : null;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
