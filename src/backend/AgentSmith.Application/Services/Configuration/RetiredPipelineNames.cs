using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// 2026-09-25-e5b1: the four coding preset names p0393 collapsed into <c>code</c>, and what each
/// one became. It is what remains of the alias map after the map itself was deleted.
/// <para>
/// This is NOT a resolver and must never become one. Nothing routes, classifies, sizes or parks a
/// run through it: <c>PipelinePresets.TryResolve</c> and <c>IsAcceptedName</c> do not consult it,
/// which is the whole point — a name in here does not run. It has exactly two
/// readers, and both of them are addressed to a PERSON: the one-shot migration that rewrites a
/// stored configuration off these names, and the startup finding that tells an operator holding
/// one what to write instead.
/// </para>
/// </summary>
public static class RetiredPipelineNames
{
    /// <summary>Retired name -> the preset that replaced it.</summary>
    public static readonly IReadOnlyDictionary<string, string> Replacements =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fix-bug"] = PipelinePresets.CodeName,
            ["fix-no-test"] = PipelinePresets.CodeName,
            ["add-feature"] = PipelinePresets.CodeName,
            ["phase-execution"] = PipelinePresets.CodeName,
        };

    /// <summary>The preset that replaced <paramref name="pipelineName"/>, or null when the name
    /// was never one of the retired four.</summary>
    public static string? ReplacementFor(string pipelineName) =>
        Replacements.GetValueOrDefault(pipelineName);
}
