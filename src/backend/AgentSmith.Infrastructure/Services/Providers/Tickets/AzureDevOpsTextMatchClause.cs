namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: the WIQL clause matching typed text against a work item's title and body.
/// In a file of its own because the discovery WIQL builder's only entry point takes a
/// DiscoveryQuery, which carries no text, and the lister that would otherwise host this sits at
/// its file-length baseline.
/// <para>
/// TWO OPERATORS, NOT ONE. <c>CONTAINS WORDS</c> is the full-text, whole-word operator and needs
/// the field indexed — a typed "auth" does not find "authentication", which is most of what a
/// person types into a picker. <see cref="System.String"/>-typed <c>System.Title</c> takes plain
/// <c>CONTAINS</c>, which is a substring match; the long-text fields take <c>CONTAINS WORDS</c>
/// because that is the only operator they support.
/// </para>
/// <para>
/// REPRO STEPS IS THE THIRD FIELD, NOT AN EXTRA. A Bug's body lives in
/// <c>Microsoft.VSTS.TCM.ReproSteps</c> rather than in <c>System.Description</c> — the lister
/// already hydrates it for that reason — so a Description-only clause would match no Bug's body
/// on the whole board.
/// </para>
/// </summary>
internal static class AzureDevOpsTextMatchClause
{
    public static string? For(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0) return null;
        var value = AzureDevOpsOpenScope.Escaped(trimmed);
        return $"([System.Title] CONTAINS '{value}' "
            + $"OR [System.Description] CONTAINS WORDS '{value}' "
            + $"OR [Microsoft.VSTS.TCM.ReproSteps] CONTAINS WORDS '{value}')";
    }
}
