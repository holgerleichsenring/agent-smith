using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042ed: what a look may conclude from a READ-ONLY source scope.
/// <para>
/// A source scope refuses what it will not serve with exit 1 (<see cref="Sandbox.SourceScopeRefusal"/>),
/// and a scope that could not clone answers every step the same way. Read as a command's exit,
/// that refusal says "grep found nothing" and "the audit found nothing" — a look that never
/// ran, standing as proof of absence. So: the scope is opened FIRST (the typed failure escapes
/// <see cref="ISourceScopeSandbox.MaterializeAsync"/> instead of becoming an exit code), and a
/// search is a Grep step, whose exits mean the opposite of a Run grep's — 0 with an empty array
/// is "no match", anything else is a step that could not run.
/// </para>
/// <para>
/// 2026-09-22-46ef: the scope now serves a process whose program is one the server itself
/// builds, so "no Run step is ever sent to one" is no longer the rule it was. It stays the rule
/// for THIS look: everything here is a Grep or a ReadFile, because a look states absences and an
/// absence must come from a step whose exit convention it can read.
/// </para>
/// <para>
/// A sealed class over ONE scope rather than a static helper: the scope is a collaborator, and a
/// static that takes one is a service without a constructor (NoStaticStateRuleTests).
/// </para>
/// </summary>
internal sealed class SourceScopeLook(ISourceScopeSandbox scope)
{
    /// <summary>The exit a look that never ran is minted under: no command reported one.</summary>
    public const int NotRunExit = -1;

    private const int NoMatchExit = 1;
    private const int CouldNotRunExit = 2;
    private const int TimeoutSeconds = 90;

    /// <summary>Enough matching lines to settle a premise; the tool bounds the text besides.</summary>
    private const int HeadLimit = 200;

    /// <summary>Proves the scope opens, or says why it did not — nothing is run either way.
    /// Guarded on the RUN's token, not on the exception's type: a clone that times out surfaces
    /// as a TaskCanceledException while the run itself was never cancelled, and that is a scope
    /// that could not be opened, not a turn to abandon.</summary>
    public async Task<string?> TryOpenAsync(CancellationToken ct)
    {
        try
        {
            await scope.MaterializeAsync(ct);
            return null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// A search as a Grep step, answered in a Run grep's exit convention so one evidence line
    /// reads the same whichever sandbox served it: 0 matched, 1 matched nothing, 2 could not run.
    /// </summary>
    public async Task<StepResult> SearchAsync(string pattern, string under, CancellationToken ct)
    {
        // The ONE caller that searches hidden and ignored paths: an absence this look states
        // must hold over the whole checkout, and nothing here reads a result into a prompt head
        // shared with a master's own greps.
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Grep, TimeoutSeconds: TimeoutSeconds,
            Path: ContainedPath.Absolute(under), Pattern: pattern, HeadLimit: HeadLimit,
            SearchHidden: true);
        var result = await scope.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode != 0 || result.OutputContent is null)
            return result with
            {
                ExitCode = CouldNotRunExit,
                OutputContent = result.ErrorMessage ?? result.OutputContent,
            };
        var rendered = GrepResultRenderer.Render(result.OutputContent, GrepOutputMode.Content, HeadLimit);
        return rendered.Length == 0
            ? result with { ExitCode = NoMatchExit, OutputContent = null }
            : result with { OutputContent = rendered };
    }

    /// <summary>
    /// One file, read from the step result itself rather than through the reader that collapses
    /// every failure to null: after the scope has opened, the file reader's own
    /// <see cref="StepErrors.FileNotFoundPrefix"/> is an absence the reviewer may state, and
    /// anything else — including a materialisation the scope wrapped into an exit 1 — is a read
    /// that could not run. Matched on the producer's wording rather than on the words "not
    /// found" appearing anywhere in it.
    /// </summary>
    public async Task<SourceScopeRead> ReadAsync(string under, CancellationToken ct)
    {
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, TimeoutSeconds: TimeoutSeconds,
            Path: ContainedPath.Absolute(under), WorkingDirectory: Repository.SandboxWorkPath);
        var result = await scope.RunStepAsync(step, progress: null, ct);
        if (result.ExitCode == 0) return new SourceScopeRead(result.OutputContent ?? string.Empty, Ran: true);
        return StepErrors.IsFileNotFound(result.ErrorMessage)
            ? new SourceScopeRead(Content: null, Ran: true)
            : new SourceScopeRead(Content: null, Ran: false, result.ErrorMessage ?? string.Empty);
    }
}
