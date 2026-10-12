using AgentSmith.Contracts.Models;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: extracts the typed terminal outcome from a design-partner reply.
/// Same tolerant-but-fail-loud school as SpecDraftValidator: no marker at all
/// is a plain answer (never an error), a present-but-malformed marker fails
/// with the exact reason so the master can fix it on the one re-prompt.
/// </summary>
public sealed class OutcomeProposalResolver(
    ISpecDraftValidator draftValidator,
    SpecDraftReader draftReader,
    BugOutcomeParser bugParser,
    EpicOutcomeParser epicParser,
    DialogDraftRepos draftRepos) : IOutcomeProposalResolver
{
    public OutcomeResolution Resolve(string reply, IReadOnlyList<string> scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var text = reply ?? string.Empty;
        var blocks = SpecDialogDraftBlocks.OutcomeBlocks(text);
        if (blocks.Count == 0) return ResolveFromDraft(text, scope);
        if (blocks.Count > 1)
            return new OutcomeInvalid(
                $"the reply contains {blocks.Count} ```outcome blocks — emit exactly one");
        if (draftValidator.Validate(text) is not SpecDraftAbsent)
            return new OutcomeInvalid(
                "the reply mixes an ```outcome block with a bare ```yaml block — a spec "
                + "draft is the bare ```yaml block alone; bug and epic payloads live "
                + "inside the single ```outcome block");
        return ResolveOutcomeBlock(blocks[0].Groups[1].Value, scope);
    }

    // No outcome block: the p0315b contract is unchanged — a bare ```yaml
    // draft is a spec, no artifact is an answer.
    private OutcomeResolution ResolveFromDraft(string reply, IReadOnlyList<string> scope) =>
        draftValidator.Validate(reply) switch
        {
            SpecDraftAbsent => new OutcomeResolved(new AnswerOutcome()),
            SpecDraftValid valid => ResolveSpec(valid.Yaml, scope),
            SpecDraftInvalid invalid => new OutcomeInvalid(invalid.Error),
            var other => throw new InvalidOperationException(
                $"Unknown SpecDraftOutcome '{other.GetType().Name}'."),
        };

    // 2026-10-12-3a36e: the lone spec names its repositories — stated in scope, or filled.
    private OutcomeResolution ResolveSpec(string yaml, IReadOnlyList<string> scope)
    {
        var (decided, error) = draftRepos.ForSpec(yaml, scope);
        return decided is null
            ? new OutcomeInvalid(error!)
            : new OutcomeResolved(new SpecOutcome(draftReader.Read(decided)));
    }

    private OutcomeResolution ResolveOutcomeBlock(string yaml, IReadOnlyList<string> scope)
    {
        IReadOnlyDictionary<string, object?> map;
        try
        {
            map = OutcomeYamlReader.ReadMap(yaml);
        }
        catch (YamlException ex)
        {
            return new OutcomeInvalid($"the ```outcome block is not valid YAML: {ex.Message}");
        }

        var kind = OutcomeYamlReader.GetString(map, "kind");
        return kind switch
        {
            "bug" => bugParser.Parse(map),
            "epic" => epicParser.Parse(map, scope),
            "answer" => new OutcomeInvalid(
                "kind 'answer' needs no ```outcome block — reply with plain prose"),
            "phase" or "spec" => new OutcomeInvalid(
                $"kind '{kind}' needs no ```outcome block — emit the spec as the bare ```yaml block"),
            null => new OutcomeInvalid("the ```outcome block is missing 'kind' (bug | epic)"),
            _ => new OutcomeInvalid($"unknown outcome kind '{kind}' — expected bug or epic"),
        };
    }
}
