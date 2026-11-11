using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-05-862ea: state_work_picture — the design master records what the operator's work is: its kind, where its
/// truth lives, its expected size. The picture lands on the turn's reply slot and is stored when the turn is kept,
/// replacing a stored one; a refused picture comes back with the rule it broke. The last admitted call of a turn wins.
/// 2026-11-10-548f: an optional effort raises the turn's cost cap to that tier at once, which is what its review may
/// spend; the answer names the cap.
/// </summary>
public sealed class WorkPictureToolHost(
    SpecDialogReplySlot slot, Func<ComplexityTier, CancellationToken, Task<CostCapValues?>>? raise = null,
    ComplexityTier stored = ComplexityTier.Unknown) : IToolHost
{
    public const string Name = "state_work_picture";

    internal const string Stored = "Stored. State it in one line of your reply.";

    /// <summary>2026-11-10-fc66: each kind defined, so a proof of concept carried into a codebase reads as a rebuild.</summary>
    internal const string KindDescription =
        "rebuild: the behavior already exists somewhere (an upload, an old system, a proof of concept) and is carried "
        + "into this form; feature: behavior that exists nowhere yet; fix: existing behavior that is wrong; document: "
        + "writing documentation as the work.";

    /// <summary>2026-11-10-548f: what each tier means for a design turn; 2026-11-11-205fc: the rule and one observation.</summary>
    internal const string EffortDescription =
        "Required when size is series or plan. size is how the work is cut; effort is how much the review of this "
        + "turn's drafts may spend. trivial or small: one small spec; medium: one spec of real work; large: a series; "
        + "xl: a whole product or proof of concept carried into repositories. The turn's cost cap rises to this tier's "
        + "cap and never falls; a tier at or below the current cap changes nothing, and a cap the operator set for this "
        + "pipeline is the ceiling. Observed once: at the $5 default a ten-spec series had 3 of its drafts reviewed "
        + "(run 2026-11-10T19-36-22-96b7).";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(StateWorkPictureAsync, name: Name)];
    }

    [Description(
        "Record the work picture of this conversation: the kind of undertaking, where its truth lives, the "
        + "expected size and the effort. Call it again to record a correction; the last call replaces the stored picture. It is not a source read.")]
    private async Task<string> StateWorkPictureAsync(
        [Description(KindDescription)] string kind,
        [Description("One line of plain prose, at most 300 characters: where what is wanted is described (an upload, the ticket, the conversation) and what the repositories already provide.")] string truth,
        [Description("one-spec, series or plan.")] string size,
        [Description(EffortDescription)] string? effort = null,
        CancellationToken cancellationToken = default)
    {
        var (picture, refusal) = WorkPictureAdmission.Of(kind, truth, size, effort);
        if (picture is null) return $"Not stored: {refusal} State it again.";
        // 2026-11-10-548f review: a correction later in the turn replaces the picture, never the turn's own higher
        // effort — its cap already rose to it, and the keeper stores the higher of this and the stored tier.
        // 205fc review: the conversation's stored tier counts too, so a lower effort in a later turn never publishes less.
        var kept = (int)(slot.WorkPicture?.Effort ?? ComplexityTier.Unknown);
        var effective = (ComplexityTier)Math.Max((int)picture.Effort, Math.Max(kept, (int)stored));
        slot.WorkPicture = picture with { Effort = effective };
        // 2026-11-11-205fc: the raise gets the tier the slot keeps, so the published tier never falls below the cap.
        if (picture.Effort == ComplexityTier.Unknown || raise is null) return Stored;
        var cap = await raise(effective, cancellationToken);
        return cap is null ? Stored : $"{Stored} The turn's cost cap is now ${cap.Usd:0.##} / {cap.Tokens:N0} tokens.";
    }
}
