using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-05-862ea: state_work_picture — the design master records what the operator's work is: its kind, where its
/// truth lives, its expected size. The picture lands on the turn's reply slot and is stored when the turn is kept,
/// replacing a stored one; a refused picture comes back with the rule it broke. The last admitted call of a turn wins.
/// </summary>
public sealed class WorkPictureToolHost(SpecDialogReplySlot slot) : IToolHost
{
    public const string Name = "state_work_picture";

    internal const string Stored = "Stored. State it in one line of your reply.";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode)
    {
        _ = stage;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(StateWorkPicture, name: Name)];
    }

    [Description(
        "Record the work picture of this conversation: the kind of undertaking, where its truth lives, and the "
        + "expected size. Call it again to record a correction; the last call replaces the stored picture. It is not a source read.")]
    private string StateWorkPicture(
        [Description("rebuild, feature, fix or document (writing documentation as the work).")] string kind,
        [Description("One line of plain prose, at most 300 characters: where what is wanted is described (an upload, the ticket, the conversation) and what the repositories already provide.")] string truth,
        [Description("one-spec, series or plan.")] string size)
    {
        var (picture, refusal) = WorkPictureAdmission.Of(kind, truth, size);
        if (picture is null) return $"Not stored: {refusal} State it again.";
        slot.WorkPicture = picture;
        return Stored;
    }
}
