namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199: maps CLI-level preset names onto the canonical concept-vocabulary
/// values that <c>PipelineNameInitializer</c> writes. Without this mapping
/// the concept writer rejects e.g. "add-feature" — the vocab uses
/// "feature-implementation". Same rule the production initializer applies;
/// we keep it in test code so the runner stays decoupled from the writer.
/// </summary>
internal static class PipelineNameConceptMap
{
    public static string ToConceptValue(string presetName) => presetName switch
    {
        "add-feature" => "feature-implementation",
        "fix-no-test" => "fix-bug",
        _ => presetName,
    };
}
