using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Prompts;

/// <summary>
/// 2026-09-08-1830: renders the contexts the scope call named as the list the
/// derivation declares its phases against — every one is carried by a phase or listed
/// as discarded with a reason, and the reader holds the cut to that. Empty when nothing
/// was named, so the derivation reads exactly the prompt it read before.
/// </summary>
public static class ScopeNamedContextsPromptSection
{
    public static string Render(ScopeNamedContexts? named)
    {
        if (named is null || named.Contexts.Count == 0) return string.Empty;
        var listed = string.Join("\n", named.Contexts.Select(c => $"- {c}"));
        var because = string.IsNullOrWhiteSpace(named.Rationale) ? string.Empty : $" — {named.Rationale}";
        return $"""

            ## Contexts the scope call named
            The scope call read this ticket as touching the contexts below{because}. Every one of
            them is either carried by a phase — name it in that phase's "contexts", spelled as
            listed — or listed under "discarded_contexts" with the reason it is outside this
            ticket's work. A cut that covers fewer contexts than named is refused.
            {listed}
            """;
    }
}
