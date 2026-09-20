using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-13-9f84: reads what a template declares as its OWN proof — the <c>verify:</c>
/// block of its context.yaml — while the scope the derivation opened is still standing, and
/// puts the answer on the record as a framework evidence line.
/// <para>
/// A template is held up as exemplary and NOTHING checks that. Nothing can be refused for
/// failing to prove itself before the estate is countable: how many declared templates
/// declare any proof at all. None of the boilerplates carries a verify block today, so a
/// gate would refuse every one of them on day one and its cheapest exit would be one
/// trivial stage. So this reports, and a successor refuses once the count says a template
/// can pass.
/// </para>
/// <para>
/// NOTHING IS MATERIALISED HERE. 2026-09-13-84c0 decided the derivation's template scopes
/// stay lazy, because an eager clone spends a pod on every derivation of every project that
/// declares a template — including the many where nobody looks. A scope that never opened is
/// therefore not read and not reported: a line about a file nobody fetched would be a
/// measurement nobody took.
/// </para>
/// <para>
/// 2026-09-15-6f8d: and nothing is read out of a checkout its declaration does not own. Two
/// declarations of one address share one scope, so reading each of them there read the
/// SECOND declaration's template context out of the FIRST declaration's checkout — and minted
/// that as a numbered line a finding could cite.
/// </para>
/// </summary>
public sealed class TemplateProofReport(
    TemplateProofRead read, ILogger<TemplateProofReport> logger)
{
    /// <summary>
    /// One line per template the derivation actually opened, minted once per derivation.
    /// A run with no template, or one whose templates nobody opened, is silent — and never
    /// fails: what a template declares is REPORTED here, never refused.
    /// </summary>
    public async Task RecordAsync(
        DerivationLook? look, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (look is null || look.Templates.Count == 0) return;
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
            || project is null) return;
        for (var ordinal = 0; ordinal < project.Templates.Count; ordinal++)
        {
            var declared = project.Templates[ordinal];
            // 2026-09-16-4df5: through the one builder. Composed here by hand, two
            // declarations of one context name resolved to one scope and one evidence key, so
            // the second was never proven while the report read as covered.
            var name = TemplateScopeName.For(declared);
            if (!look.Templates.TryGetValue(name, out var scope) || !scope.IsMaterialized) continue;
            // The materialisation guard comes FIRST: scopes are lazy, and asking who owns an
            // address before asking whether anyone opened it would mint a skip for a template
            // nobody looked at.
            Mint(look, name, TemplateScopeName.Owns(project.Templates, ordinal)
                ? await read.OfAsync(declared, scope, cancellationToken)
                : NotItsOwn(declared, ordinal));
        }
    }

    private void Mint(DerivationLook look, string name, (string What, int Exit, string? Path) outcome)
    {
        var id = look.Evidence.RememberOnce(new EvidenceRecord(
            name, EvidenceRecord.TemplateProof, outcome.What, outcome.Exit,
            Ran: outcome.Exit == TemplateProofRead.Read, outcome.Path));
        if (id is not null) logger.LogInformation("[{Id}] {Name}: {What}", id, name, outcome.What);
    }

    // A declaration that does not own its address was never read, and says so. The ORDINAL is
    // what distinguishes it: evidence is deduplicated on the repository and what-was-done, the
    // address is the repository and is shared by every declaration of a duplicate, and two
    // losers can be identical in every other field. It is a discriminator, not a meaning — so
    // the template context is named too, because after the catalog collapse the model is shown
    // only the owner's address and "declaration two" alone names nothing it has a list of.
    private static (string What, int Exit, string? Path) NotItsOwn(
        ProjectTemplate declared, int ordinal) =>
        ($"skipped declaration {ordinal + 1} of this project's templates: it is addressed as an "
            + $"earlier declaration is, so the template context '{declared.TemplateContext}' was "
            + "never read — this declaration proves nothing about itself",
         TemplateProofRead.Unread, null);
}
