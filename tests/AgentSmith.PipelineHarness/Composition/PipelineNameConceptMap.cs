namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// Maps CLI-level preset names onto the canonical concept-vocabulary values
/// that <c>PipelineNameInitializer</c> writes. Most presets publish their own
/// name (add-feature, fix-bug, …); only fix-no-test aliases onto fix-bug so it
/// reuses the fix-bug skill roster.
///
/// NOTE: production's PipelineNameInitializer does NOT apply this map — it
/// publishes the resolved name verbatim. Harmless for identity presets, but it
/// means fix-no-test is still broken end-to-end in production (SetEnum rejects
/// "fix-no-test"); tracked separately. add-feature was the same class of bug
/// until the concept vocab + coding skills were renamed to "add-feature".
/// </summary>
internal static class PipelineNameConceptMap
{
    public static string ToConceptValue(string presetName) => presetName switch
    {
        "fix-no-test" => "fix-bug",
        _ => presetName,
    };
}
