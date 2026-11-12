using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-12-cd4ca: one spec written into a draft. Its id is placed (minted, or the spec it rewrites), its text is
/// checked against the spec schema, a non-parent spec's repositories are decided against the turn's scope, and the
/// draft with it must still form one proposal. A refusal is the exact reason, and the draft stays as it was.
/// </summary>
public sealed class SpecDraftWrite(
    SpecDraftIds ids, ISpecDraftValidator validator, DialogDraftRepos repos, SpecDraftReader reader, SpecDraftShape shape)
{
    public DraftWrite Write(ProposalDraft draft, string yaml, bool parent, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(scope);
        if (DraftKind.Conflict(draft, DraftKind.Specs) is { } conflict) return DraftWrite.Refused(conflict);
        var (placed, id, error) = ids.Place(draft, yaml);
        if (placed is null) return DraftWrite.Refused(error!);
        var validation = validator.ValidateYaml(placed);
        if (validation is SpecDraftInvalid invalid) return DraftWrite.Refused($"spec '{id}': {invalid.Error}");
        if (validation is not SpecDraftValid valid) return DraftWrite.Refused($"spec '{id}' is empty");
        var (decided, reposError) = parent ? (valid.Yaml, null) : repos.ForSpec(valid.Yaml, scope);
        if (decided is null) return DraftWrite.Refused($"spec '{id}': {reposError}");
        return shape.Place(draft, new DraftSpec(reader.Read(decided), parent)) with { Handle = id };
    }
}
