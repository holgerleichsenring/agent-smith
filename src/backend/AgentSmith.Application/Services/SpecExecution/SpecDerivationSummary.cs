using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.SpecExecution;

/// <summary>
/// 2026-10-26-bdd0d: the sentence a spec derivation ends on — where its specs are. A preview wrote nothing by
/// choice; a written revision sets the revision sha on the context, and only a written one; without one the branch write did not complete.
/// </summary>
public static class SpecDerivationSummary
{
    public static string Of(PipelineContext pipeline, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(set);
        var specs = set.Specs.Count == 1 ? "1 spec" : $"{set.Specs.Count} specs";
        if (pipeline.Flag(ContextKeys.SpecDerivationPreview)) return $"{specs} derived as a preview; nothing was written.";
        return pipeline.Has(ContextKeys.SpecRevisionSha)
            ? $"{specs} derived and stored on the ticket branch."
            : $"{specs} derived; the branch write did not complete, so nothing was stored.";
    }
}
