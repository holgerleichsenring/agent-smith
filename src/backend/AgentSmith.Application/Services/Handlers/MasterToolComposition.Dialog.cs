using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-11-18-9c1cd: the design partner's and the steward's surface: the same tools on every turn of a conversation.</summary>
public sealed partial class MasterToolComposition
{
    private IList<AITool> DialogMaster(
        FilesystemToolHost fs, MasterRecordTools records, WebToolHost? web, WithdrawFiledTicketToolHost? withdraw,
        AgenticMasterContext context, Func<bool, IReadOnlyList<AITool>, AIFunction?> ticket)
    {
        var pipeline = context.Pipeline;
        var steward = context.IsTicketSteward;
        var mayStartRuns = DesignWorkToolGate.MayStartRuns(pipeline);
        var acts = _acts.Tools(context);
        var references = _references.Tools(pipeline, isDesignTurn: true);
        var work = _work.Tools(pipeline, records).ToList();
        var notes = ConversationNotesToolHost.For(pipeline); // 2026-11-12-cd4ck
        IList<AITool> master =
        [
            MergedTools.ReadFile(fs, MergedTools.Named(references, "view_reference_image"),
                MergedTools.Named(references, "read_mock"), notes, design: true),
            .. _surface.SpecDialog(fs, web: null).Where(t => t.Name != "read_file"),
            MergedTools.WebFetch(web, _design.Create(pipeline)),
            .. MergedTools.Named(references, "render_reference") is { } render
                ? [MergedTools.RenderReference(render, MergedTools.Named(references, "compare_reference"))]
                : Array.Empty<AITool>(),
            MergedDialogTools.RunCommand(MergedTools.Named(work, "run_command"), MergedTools.Named(references, "run_in_reference"), mayStartRuns),
            MergedDialogTools.WriteFile([.. acts, .. references], notes, steward),
            MergedDialogTools.EditFile(acts, notes, steward),
            MergedDialogTools.NameConversation(MergedTools.Named(acts, NameConversationToolHost.Name)),
            records.Recall.GetTools(null, null).Single(),
            MergedTools.Remember(
                mayStartRuns && records.ProjectRemember is { } project ? (type, name, description, body, _, ct) => project.Remember(type, name, description, body, ct) : null,
                mayStartRuns ? "Not recorded: this installation keeps no project memory." : MergedDialogTools.PosterMayNot,
                records.Dispute, RememberDescription, withRepository: false),
        ];
        if (!steward)
            master = [.. master,
                MergedDialogTools.StateWorkPicture(MergedTools.Named(acts, WorkPictureToolHost.Name), MergedTools.Named(acts, OpenQuestionsToolHost.Name)),
                .. MergedTools.Named(acts, OfferFilingToolHost.Name) is { } offer ? [offer] : Array.Empty<AITool>(),
                MergedDialogTools.Withdraw(withdraw is null ? null : MergedTools.Named(withdraw.GetTools(null, null), "withdraw_filed_ticket"))];
        // The bound-ticket acts join the design MASTER outside BaseSurface: a child acts on no one's word.
        if (ticket(true, acts) is { } read)
            master = [.. master, read, MergedDialogTools.StartNextAttempt(
                MergedTools.Named(acts, "start_next_attempt"), MergedTools.Named(acts, DeriveSpecsToolHost.Name), mayStartRuns)];
        if (steward)
            master = [.. master, MergedDialogTools.EditSeries(MergedTools.Named(acts, "edit_series"), mayStartRuns),
                .. acts.Where(t => t.Name is "read_run_events" or "read_config")]; // 2026-11-18-d6d2b
        return master;
    }
}
