namespace AgentSmith.Application.Prompts;

/// <summary>
/// p0415: THE table. Every prompt name the server requests under a fixed name has
/// exactly one owner declared here, and <see cref="SkillCatalogPromptCatalog"/>
/// reads this table before it looks anywhere. Resolution ORDER decides nothing:
/// a catalog-owned name never falls back to an embedded resource (p0205), and an
/// embedded-owned name never consults the master catalog — which is what let a
/// catalog master of the same name silently win before this phase.
/// <para>
/// A name that is NOT listed is a handler-passed master-skill name
/// (security-master, pr-review-master, ...) and keeps the direct-master lookup.
/// </para>
/// </summary>
public static class PromptOwnership
{
    private static readonly IReadOnlyDictionary<string, PromptOwner> Table =
        new Dictionary<string, PromptOwner>(StringComparer.Ordinal)
        {
            // p0179a: migrated to master skills. coding-agent-master carries the
            // agent-execute-system body plus the p0177 step-11 sub-agent guidance.
            // A new master skill gets its entry here together with the
            // corresponding cross-repo SKILL.md.
            ["agent-execute-system"] = PromptOwner.Catalog("coding-agent-master"),
            ["project-analyzer-system"] = PromptOwner.Catalog("project-analyzer-master"),
            ["knowledge-system"] = PromptOwner.Catalog("knowledge-master"),
            ["contract-classifier-system"] = PromptOwner.Catalog("contract-classifier-master"),
            ["context-generator-system"] = PromptOwner.Catalog("context-generator-master"),
            ["context-quality-template"] = PromptOwner.Catalog("context-generator-master"),

            // p0415: the embedded resource carries the ships_code obligation
            // (p0400a) and the cut-sizing rule (p0413); no released catalog carries
            // both yet, and the pinned catalog ships no master of this name at all.
            // Ownership moves to the catalog when a parity release exists AND the
            // pin moves — NoPromptName_HasTwoOwners fails at exactly that moment.
            ["spec-derivation-master"] = PromptOwner.Embedded,
            ["expectation-drafting-system"] = PromptOwner.Embedded,
        };

    /// <summary>
    /// p0324: the masters the table hard-requires. The skills-catalog preflight
    /// check verifies each one is present in the loaded catalog so a stale pin
    /// fails at doctor/startup time, not mid-run.
    /// </summary>
    public static IReadOnlyCollection<string> RequiredMasterSkills { get; } =
        [.. Table.Values
            .Where(o => o.Source == PromptSource.SkillCatalog)
            .Select(o => o.MasterSkillName)
            .Distinct(StringComparer.Ordinal)];

    /// <summary>The names this table declares an owner for.</summary>
    public static IReadOnlyCollection<string> DeclaredNames { get; } = [.. Table.Keys];

    /// <summary>Names the embedded resources this table owns outright.</summary>
    public static IReadOnlyCollection<string> EmbeddedOwnedNames { get; } =
        [.. Table.Where(e => e.Value.Source == PromptSource.EmbeddedResource).Select(e => e.Key)];

    public static bool TryGetOwner(string promptName, out PromptOwner owner) =>
        Table.TryGetValue(promptName, out owner!);
}
