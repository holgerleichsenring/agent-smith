namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-25-8e51e: WHICH Azure DevOps field holds a work item's body — the one question a read
/// and a write must never answer differently.
/// <para>
/// p0318 found the read half: a Bug keeps its body in
/// <c>Microsoft.VSTS.TCM.ReproSteps</c> and its <c>System.Description</c> is empty, so reading
/// Description alone handed the planner a title and it invented the scope. The write half is this
/// phase's: a rewrite that put the amended text into Description while the Bug's body sits in
/// ReproSteps would give the ticket a SECOND body, invisible to every reader — including the one
/// that reads it back through <see cref="AzureDevOpsFieldMapper"/>, which prefers Description.
/// </para>
/// <para>
/// Decided from the work item's OWN fields rather than from a table of work-item types: a process
/// template can put the body anywhere, and the field that is populated is the field that type
/// uses. Both halves call this, so they cannot drift apart.
/// </para>
/// </summary>
public static class AzureDevOpsBodyField
{
    /// <summary>What every type uses unless its own fields say otherwise.</summary>
    public const string Description = "System.Description";

    /// <summary>Where a Bug keeps its body.</summary>
    public const string ReproSteps = "Microsoft.VSTS.TCM.ReproSteps";

    /// <summary>The last fallback, for the templates that use it as the body.</summary>
    public const string SystemInfo = "Microsoft.VSTS.TCM.SystemInfo";

    /// <summary>
    /// The reference name of the field this work item keeps its body in: the first of
    /// Description, reproduction steps and system info that holds anything, and Description when
    /// the work item has no body at all.
    /// </summary>
    public static string Of(IDictionary<string, object> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        foreach (var name in new[] { Description, ReproSteps, SystemInfo })
            if (!string.IsNullOrWhiteSpace(Read(fields, name))) return name;
        return Description;
    }

    /// <summary>The field's text, empty when the work item does not carry it.</summary>
    public static string Read(IDictionary<string, object> fields, string name)
    {
        ArgumentNullException.ThrowIfNull(fields);
        return fields.TryGetValue(name, out var value) ? value?.ToString() ?? "" : "";
    }
}
