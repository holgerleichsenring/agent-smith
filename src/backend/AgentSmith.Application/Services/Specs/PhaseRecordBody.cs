using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: the text of a phase record — the ratified spec, followed by the phase's own
/// account of what came of it. The spec says what was asked; without the account the
/// record cannot say what was delivered.
/// </summary>
public static class PhaseRecordBody
{
    public static string For(PhaseDraft draft, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(pipeline);
        var account = pipeline.TryGet<IReadOnlyList<SpecAccount>>(
            ContextKeys.PhaseAccounts, out var accounts) && accounts is not null
                ? SpecAccountRenderer.ToMarkdown(accounts)
                : string.Empty;

        var spec = draft.Yaml.TrimEnd() + "\n";
        if (account.Length == 0) return spec;
        return spec + "\n# " + account.Replace("\n", "\n# ").TrimEnd() + "\n";
    }
}
