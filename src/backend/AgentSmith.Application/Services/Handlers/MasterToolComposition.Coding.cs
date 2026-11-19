using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>2026-11-18-9c1cd: the coding and scan masters' surfaces, merged tools in place of the ones they replace.</summary>
public sealed partial class MasterToolComposition
{
    private IList<AITool> ScanMaster(IList<AITool> surface, MasterRecordTools records) =>
        Replace(surface, RememberOf(records, withRepository: true));

    private IList<AITool> CodingMaster(
        IList<AITool> surface, FilesystemToolHost fs, MasterRecordTools records, WebToolHost? web,
        ProgressLedgerToolHost progress, AgenticMasterContext context)
    {
        var pipeline = context.Pipeline;
        var references = _references.Tools(pipeline, isDesignTurn: false);
        var master = Replace(surface,
            MergedTools.ReadFile(fs, MergedTools.Named(references, "view_reference_image")),
            MergedTools.LogDecision(records.Log, records.DefaultDecision),
            RememberOf(records, withRepository: true),
            MergedTools.CodingRunCommand(fs, _gate, pipeline));
        // 2026-10-01-7f7ab: a Figma link is read through web_fetch on a project with a design source — the MASTER only:
        // children fanning out over one file would multiply calls against one rate limit.
        var design = _design.Create(pipeline);
        if (web is not null || design is not null) master = Replace(master, MergedTools.WebFetch(web, design), append: true);
        // 2026-10-01-283df: render_reference on a project whose config enables the browser, comparing as well.
        if (MergedTools.Named(references, "render_reference") is { } render)
            master = [.. master, MergedTools.RenderReference(render, MergedTools.Named(references, "compare_reference"))];
        // p0331 ensure_repo_sandbox, p0341 update_progress; a spec's master only: revise_spec, finish_spec.
        master = [.. master, .. _ensure.Create(pipeline, fs, _logger).GetTools(null, null),
            .. progress.GetTools(null, null), .. records.ReviseSpec?.GetTools(null, null) ?? []];
        if (records.FinishSpec is { } finish) master = [.. master, MergedTools.FinishSpec(finish, records.HandForward)];
        return master;
    }

    private const string RememberDescription =
        "Keeps what the person said that holds beyond this turn — how they want work done, a constraint of the project, "
        + "a fact no repository records — as a memory of the PROJECT, which every later conversation and run sees, quoted "
        + "with this conversation as its source; an existing name is updated. With dispute it instead disputes the stored "
        + "memory `name` that led you wrong. A memory is a hint with a track record, never a rule.";

    private static AITool RememberOf(MasterRecordTools records, bool withRepository) =>
        MergedTools.Remember(
            (type, name, description, body, repository, ct) => records.Remember.Remember(type, name, description, body, repository, ct),
            string.Empty, records.Dispute,
            records.Remember.GetTools(null, null).Single().Description
                + " With dispute it instead disputes the stored memory `name` that led you wrong: what it got wrong, a sentence or two.",
            withRepository);

    // Replaces each same-named tool in place; append adds one the surface lacks.
    private static IList<AITool> Replace(IList<AITool> surface, AITool replacement, bool append) =>
        surface.Any(t => t.Name == replacement.Name)
            ? [.. surface.Select(t => t.Name == replacement.Name ? replacement : t)]
            : append ? [.. surface, replacement] : surface;

    private static IList<AITool> Replace(IList<AITool> surface, params AITool[] replacements) =>
        replacements.Aggregate(surface, (current, replacement) => Replace(current, replacement, append: false));
}
