using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-6f35: opens the templates a phase is built after for the master that types
/// the code. The derivation names HOW a phase is to be built; until this ran, the writer
/// could not open the thing the cut cited — a citation, not a mechanism.
/// <para>
/// It is its own type because AgenticMasterHandler sits one line under its length ratchet,
/// and because opening a foreign checkout is a lifetime (materialise, address, dispose),
/// not a line in a handler that already orchestrates a loop.
/// </para>
/// </summary>
public sealed class MasterTemplateScopes(
    ProjectTemplateScopes scopes, ILogger<MasterTemplateScopes> logger)
{
    /// <summary>
    /// Selects the templates declared for the contexts THIS PHASE changes, materialises each
    /// one at its declared revision, and publishes their addresses on
    /// <see cref="ContextKeys.TemplateAddresses"/>.
    /// <para>
    /// Materialisation happens here, before the master's first token, because a template that
    /// cannot be reached must fail the phase rather than answer every read with a refusal the
    /// model spends a pass discovering. It is a defensive assert and not a second user-facing
    /// path: DeriveSpec precedes the master in the code preset and 2026-09-13-84c0 already
    /// failed the derivation on the same condition, so an operator has seen it once already.
    /// </para>
    /// </summary>
    /// <param name="readOnlySurface">
    /// A scan or a spec-dialog turn, which writes no code and therefore needs no template —
    /// the same gate the toolchain probe uses, so neither surface pays for a clone.
    /// </param>
    public async Task<MasterTemplateAttachment> OpenAsync(
        PipelineContext pipeline, PhaseDraft? draft, bool readOnlySurface,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (readOnlySurface) return MasterTemplateAttachment.Empty;
        var selected = scopes.For(pipeline, draft?.Contexts);
        if (selected.Count == 0) return MasterTemplateAttachment.Empty;

        var attachment = new MasterTemplateAttachment(selected);
        try
        {
            foreach (var (name, scope) in selected)
            {
                var sha = await scope.MaterializeAsync(cancellationToken);
                logger.LogInformation(
                    "Template '{Name}' is open to the master at {Sha}",
                    name, string.IsNullOrEmpty(sha) ? "its own default" : sha);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await attachment.DisposeAsync();
            throw Unreachable(selected.Keys, ex);
        }

        pipeline.Set<IReadOnlyList<string>>(ContextKeys.TemplateAddresses, attachment.Names);
        return attachment;
    }

    // The phase fails naming the template AND the revision it asked for: "the template is
    // broken" and "this project pinned a tag that no longer exists" are different repairs,
    // and a message that names only the repository leaves the operator to guess which.
    private static InvalidOperationException Unreachable(IEnumerable<string> names, Exception cause)
    {
        var declared = cause is SourceScopeUnavailableException typed
            ? $"'{typed.RepoName}' at '{typed.Revision ?? "its own default revision"}'"
            : "its declared repository";
        return new InvalidOperationException(
            $"A template this phase is built after could not be opened: {declared}. "
            + $"The phase cannot be implemented against a template nobody can read "
            + $"(addresses asked for: {string.Join(", ", names)}). {cause.Message}",
            cause);
    }
}
