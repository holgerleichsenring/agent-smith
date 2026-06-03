namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199d: selects the ISkillsCatalogPath the harness wires. Stub points at
/// an empty temp directory — the historical default for fix-bug / add-feature
/// fast-tier tests that assert handler shape downstream of LoadSkills without
/// needing populated AvailableRoles. Fixture points at the checked-in
/// SkillsCatalog tree (Fixtures/SkillsCatalog/skills) so YamlSkillLoader
/// walks real role definitions and BootstrapDispatch / StructuredTriage see
/// non-empty AvailableRoles. Mirrors the existing SandboxBackend pattern.
/// </summary>
public enum SkillsBackend
{
    Stub,
    Fixture,
}
