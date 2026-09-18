using System.Text.Json;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ej: reads back the <c>phase_review:&lt;phase id&gt;</c> artifact
/// 2026-09-17-042eh stores — one <see cref="PhaseReviewReport"/> per phase, written by
/// <c>PhaseReviewPublisher</c>. The naming policy mirrors the publisher's so a member added
/// later stays readable; every member today is a single word, so what makes this work is the
/// round-trip the tests take through the publisher, not the policy.
/// <para>
/// A review that was NOT TAKEN is the loud case and must survive the trip: recorded as a bare
/// list it would be indistinguishable from a review that read the whole diff and objected to
/// nothing, and the page would report "nothing found" over a question nobody asked. A row
/// that cannot be READ is a state of its own for the same reason — collapsed onto "no row" it
/// would turn a phase whose review kept three findings into a phase nobody reviewed.
/// </para>
/// </summary>
public sealed class FiledWorkPhaseReviews(ILogger<FiledWorkPhaseReviews> logger)
{
    private static readonly JsonSerializerOptions Options =
        new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>
    /// The phase's review, or null when no row was stored — the phase never reached its
    /// review step. A row that cannot be deserialized comes back as unreadable, never as
    /// absent and never as clean.
    /// </summary>
    public FiledWorkReviewView? Of(string? json, string phaseId)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var report = JsonSerializer.Deserialize<PhaseReviewReport>(json, Options);
            return report is null ? Unreadable(phaseId, null) : Taken(report);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Unreadable(phaseId, ex);
        }
    }

    /// <summary>
    /// The report as the page needs it. The findings travel whether or not the review was
    /// taken, and a "not taken" that recorded no reason still says something: the producer's
    /// own <c>PhaseReviewReport.None</c> is publishable and carries none.
    /// </summary>
    private static FiledWorkReviewView Taken(PhaseReviewReport report) =>
        new(report.Reviewed,
            report.Reviewed ? null : report.Why ?? FiledWorkReviewView.NoReasonRecorded,
            report.Findings ?? []);

    private FiledWorkReviewView Unreadable(string phaseId, Exception? ex)
    {
        logger.LogWarning(
            ex, "The phase review stored for {PhaseId} is unreadable — shown as unreadable", phaseId);
        return new FiledWorkReviewView(
            false, FiledWorkReviewView.CouldNotBeRead, [], Unreadable: true);
    }
}
