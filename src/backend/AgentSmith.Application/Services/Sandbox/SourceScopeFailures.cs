using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-13-9802: why a read-only source scope could not be prepared, said so an operator
/// reads an action rather than git. 2026-09-22-2d11b: its own file, because the refresh rung
/// classifies a remote failure exactly as the clone does and a second copy of these markers
/// would be a second answer to one question.
/// </summary>
internal static class SourceScopeFailures
{
    /// <summary>
    /// The host answering "no such repository" for a private one it will not admit to is
    /// indistinguishable from a missing repository, and the action is the same either way:
    /// look at the token. Auth is therefore matched BEFORE transport, because git wraps
    /// both in "unable to access".
    /// </summary>
    private static readonly string[] AuthMarkers =
    [
        "authentication failed", "could not read username", "invalid username or password",
        "403", "401", "permission denied", "access denied", "terminal prompts disabled",
        "repository not found",
    ];

    /// <summary>
    /// Auth first, transport second, and transport again for anything unrecognised: git
    /// wraps every remote problem in "unable to access", so the markers are what separate
    /// them, and a step that failed for a reason neither list knows is still a step that
    /// did not reach its remote. The raw git text rides along in the message either way.
    /// </summary>
    public static SourceScopeFailureKind KindOf(StepResult result)
    {
        var text = Text(result).ToLowerInvariant();
        return AuthMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal))
            ? SourceScopeFailureKind.Unauthorised
            : SourceScopeFailureKind.Unreachable;
    }

    public static string Text(StepResult result) =>
        string.Join(" ", new[] { result.ErrorMessage, result.OutputContent }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    public static SourceScopeUnavailableException Fail(
        SourceScopeFailureKind kind, RepoConnection repo, string? revision, string message) =>
        new(kind, repo.Name, revision,
            $"'{repo.Name}'{(revision is null ? string.Empty : $" at '{revision}'")}: {message}");
}
