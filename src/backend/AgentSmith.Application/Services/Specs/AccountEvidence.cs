using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-08-25-6f12: what every accounting call is shown and may do — everything except the
/// diff body it is asked about.
/// <para>
/// The body is the one part that differs between passes: a diff window for the windowed
/// pass, nothing at all for the full-reach pass, which settles by searching instead. Holding
/// the rest together is what makes a third pass expressible — the same five facts were being
/// threaded through every call site one parameter at a time, and a pass that had to carry
/// them plus its own arguments could not be written at all.
/// </para>
/// </summary>
/// <param name="DeliveryFiles">The whole delivery's file list, never one window's.</param>
/// <param name="CommandResults">The commands that really ran against the branch.</param>
/// <param name="Searchable">The repositories a search may name, for the prompt to list.</param>
/// <param name="BaseSearchable">Those whose BASE resolved a real ref, which is what the third
/// disposition rests on.</param>
/// <param name="Tools">The search tools, or null where there is no sandbox to look in.</param>
public sealed record AccountEvidence(
    CitedFileIndex DeliveryFiles,
    IReadOnlyList<string> CommandResults,
    IReadOnlyList<string>? Searchable,
    IReadOnlyList<string>? BaseSearchable,
    IList<AITool>? Tools)
{
    /// <summary>What one delivery offers: the file list read off the whole diff, and the
    /// reach the standing sandboxes give.</summary>
    internal static AccountEvidence For(
        string diff, IReadOnlyList<string> commandResults, BranchSearch? search) =>
        new(CitedFileIndex.FromDiff(diff), commandResults,
            search?.Repositories, search?.BaseSearchable, AccountTools.For(search));
}
