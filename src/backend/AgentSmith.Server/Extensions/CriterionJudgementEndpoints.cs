using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-25-e257: where the operator says a criterion's disposition was wrong.
/// <para>
/// A false negative is invisible and therefore unpriced: a run refused over a criterion the
/// branch satisfied looks exactly like a run refused correctly. Fourteen phases have tuned
/// the delivery account, each on a single failed run, because the only failures that announce
/// themselves are mechanical ones. The operator already knows which verdicts were wrong.
/// Nothing has ever asked.
/// </para>
/// <para>
/// Nothing here moves the run. No state, no verdict, no pull request — recording a judgement
/// is a statement about the past, and a button that also shipped things would be pressed for
/// reasons that have nothing to do with whether the account was right.
/// </para>
/// </summary>
internal static class CriterionJudgementEndpoints
{
    internal static WebApplication MapCriterionJudgementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/{runId}/acceptance", GetAsync).Needs(Permissions.RunsRead);
        app.MapPost("/api/runs/{runId}/judgements", RecordAsync).Needs(Permissions.RunsControl);
        app.MapDelete("/api/runs/{runId}/judgements", WithdrawAsync).Needs(Permissions.RunsControl);
        return app;
    }

    private static async Task<IResult> GetAsync(
        string runId, CriterionJudgementRepository repository, CancellationToken ct) =>
        Results.Ok(await repository.AcceptanceForRunAsync(runId, ct));

    private static async Task<IResult> RecordAsync(
        string runId, CriterionJudgementRequest request,
        CriterionJudgementRepository repository, HttpContext http, CancellationToken ct)
    {
        if (Invalid(request) is { } problem) return Results.BadRequest(new { error = problem });

        await repository.RecordAsync(
            runId, request, AuthorOf(http), DateTimeOffset.UtcNow, ct);
        return Results.Ok(await repository.AcceptanceForRunAsync(runId, ct));
    }

    private static async Task<IResult> WithdrawAsync(
        string runId, string criterion, CriterionJudgementRepository repository,
        CancellationToken ct) =>
        await repository.WithdrawAsync(runId, criterion, ct)
            ? Results.Ok(await repository.AcceptanceForRunAsync(runId, ct))
            : Results.NotFound(new { error = "no judgement is recorded for that criterion" });

    /// <summary>
    /// A reason is REQUIRED: a label without one cannot be audited later, and an unauditable
    /// label is worse than none. A status outside the vocabulary would make the corpus
    /// unreadable by anything that scores it.
    /// </summary>
    internal static string? Invalid(CriterionJudgementRequest request)
    {
        if (request is null) return "a judgement needs a body";
        if (string.IsNullOrWhiteSpace(request.Criterion)) return "a judgement names its criterion";
        if (string.IsNullOrWhiteSpace(request.Reason))
            return "a judgement states why — a label nobody can audit is worse than none";
        if (!Known(request.MachineStatus)) return "the machine status is not a known disposition";
        if (!Known(request.HumanStatus)) return "the human status is not a known disposition";
        if (string.Equals(request.MachineStatus, request.HumanStatus, StringComparison.Ordinal))
            return "an overrule states a disposition that differs from the account's";
        return null;
    }

    private static bool Known(string? status) =>
        status is AcceptanceCriterionStatuses.Met or AcceptanceCriterionStatuses.Unmet
            or AcceptanceCriterionStatuses.NotApplicable or AcceptanceCriterionStatuses.Unproven;

    /// <summary>A corpus of judgements with no author cannot be weighted, questioned or
    /// withdrawn — so an unauthenticated caller is still named, as unknown.</summary>
    private static string AuthorOf(HttpContext http) =>
        http.User.Identity?.Name is { Length: > 0 } name ? name : "unknown";
}
