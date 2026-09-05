using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-04-ae3a: what the bootstrap probe found in ONE context. The probe folded these
/// into three booleans per sandbox, which is what made the refusal unable to name anything:
/// by the time the handler saw the answer, which context contributed which false was gone.
/// </summary>
public sealed record ContextProbeResult(
    string ContextName, bool ContextYaml, bool Principles, bool RetiredPrinciples)
{
    public bool Complete => ContextYaml && Principles;

    /// <summary>The bootstrap files this context does not carry, in probe order.</summary>
    public IEnumerable<MissingBootstrapFile> Missing()
    {
        if (!ContextYaml)
            yield return new MissingBootstrapFile(ContextName, ProjectMetaPaths.ContextYamlFile, false);
        if (!Principles)
            yield return new MissingBootstrapFile(
                ContextName, ProjectMetaPaths.PrinciplesFile, RetiredPrinciples);
    }
}

/// <summary>One bootstrap file one context does not carry.</summary>
public sealed record MissingBootstrapFile(string ContextName, string File, bool RetiredPresent)
{
    public string Describe() =>
        $"'{ContextName}' has no {File}"
        + (RetiredPresent
            ? $" and carries the retired {ProjectMetaPaths.RetiredPrinciplesFile} instead: it was "
              + "initialised before the rename, so re-run init-project to compose it — the file now "
              + "also holds this project's environment rules"
            : string.Empty);
}
