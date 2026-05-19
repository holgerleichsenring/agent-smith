namespace AgentSmith.Contracts.Commands;

/// <summary>
/// API-security pipeline command names: Swagger load, the three external scanners
/// (Nuclei / Spectral / ZAP), the API-skill discussion round, the finding compile +
/// compress steps, and the deliver-findings output.
/// </summary>
public static partial class CommandNames
{
    public const string LoadSwagger = "LoadSwaggerCommand";

    public const string SpawnNuclei = "SpawnNucleiCommand";
    public const string SpawnSpectral = "SpawnSpectralCommand";
    public const string SpawnZap = "SpawnZapCommand";

    public const string ApiSecuritySkillRound = "ApiSecuritySkillRoundCommand";

    public const string CompileFindings = "CompileFindingsCommand";
    public const string DeliverFindings = "DeliverFindingsCommand";

    public const string CompressApiScanFindings = "CompressApiScanFindingsCommand";
}
