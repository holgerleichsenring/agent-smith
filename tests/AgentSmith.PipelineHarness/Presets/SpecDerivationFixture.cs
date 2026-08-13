namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0393a: the derivation reply every code-preset harness run needs. DeriveSpec runs
/// between AnalyzeCode and PhaseSpecGate and drains one FIFO slot, so every scripted
/// code run starts with this.
/// </summary>
internal static class SpecDerivationFixture
{
    /// <summary>
    /// One phase, everything carried. The segment ids are generous on purpose: ids the
    /// ticket does not have are dropped by the accounting builder, so this stays complete
    /// whatever the fixture ticket's shape is.
    /// </summary>
    public const string DerivationJson = """
        {"phases": [
           {"slug": "reject-empty-payloads",
            "goal": "Reject empty payloads with a 400 instead of a 500",
            "steps": [{"id": "guard", "action": "Answer an empty request body with 400"}],
            "done": ["An empty request body is answered with 400.",
                     "Existing callers stay unaffected."],
            "carries": [1,2,3,4,5,6,7,8,9,10,11,12], "ships_code": true}],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    /// <summary>Two phases, so a test can watch the sequence run more than one block.</summary>
    public const string TwoPhaseJson = """
        {"phases": [
           {"slug": "introduce-the-guard",
            "goal": "Introduce the empty-payload guard",
            "steps": [{"id": "guard", "action": "Answer an empty request body with 400"}],
            "done": ["An empty request body is answered with 400."],
            "carries": [1,2,3,4,5,6], "ships_code": true},
           {"slug": "migrate-the-callers",
            "goal": "Move the existing callers onto the guard",
            "steps": [{"id": "callers", "action": "Route every caller through the guard"}],
            "done": ["No caller builds its own empty-payload check."],
            "carries": [7,8,9,10,11,12], "ships_code": true}],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    /// <summary>A derivation that refuses the ticket as unbuildable.</summary>
    public const string NotImplementableJson = """
        {"phases": [],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "not_implementable",
                      "reason": "The ticket asks for a rollback of a schema that was never shipped."}}
        """;

    /// <summary>A derivation that found the requirement contradicting the repository.</summary>
    public const string ContradictsRepositoryJson = """
        {"phases": [],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "requirements_contradict_repository",
                      "reason": "The ticket renames a client this repository does not contain."}}
        """;
}
