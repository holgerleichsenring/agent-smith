namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863e: checks one declared environment by the template's delivery. A GitOps environment needs its manifest
/// and version key; a pipeline environment needs inputs that carry the version and name only known placeholders, and
/// may name a manifest and version key together — read as a promotion source only.
/// </summary>
internal static class DeclaredEnvironmentReader
{
    public static (DeclaredEnvironment? Environment, string? Error) Read(
        RawDeclaredEnvironment raw, int index, string delivery, IEnumerable<string> parameters)
    {
        if (string.IsNullOrWhiteSpace(raw.Name)) return (null, $"environments[{index}] has no name");
        var name = raw.Name.Trim();
        var (manifest, key) = (Blank(raw.Manifest), Blank(raw.VersionKey));
        var pipeline = delivery == DeploymentDeclaration.PipelineTrigger;
        if (!pipeline && manifest is null) return (null, $"environment '{name}' has no manifest");
        if ((manifest is null) != (key is null))
            return (null, $"environment '{name}' has {(manifest is null ? "a version_key but no manifest" : "no version_key")}");
        var declared = new DeclaredEnvironment(name, manifest, key, Blank(raw.PromotesFrom), pipeline ? Pipeline(raw) : null);
        var inputs = declared.Pipeline?.Inputs;
        var error = inputs is null ? null : PipelineInputValues.Check(name, inputs, parameters);
        return error is null ? (declared, null) : (null, error);
    }

    private static DeclaredPipeline Pipeline(RawDeclaredEnvironment raw) =>
        new(Blank(raw.Pipeline), Blank(raw.Ref), (raw.Inputs ?? []).ToDictionary(i => i.Key.Trim(), i => i.Value ?? string.Empty));

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
