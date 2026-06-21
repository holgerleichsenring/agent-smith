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

    /// <summary>p0267: scrapes the api-security-master's triaged observation-array
    /// answer into ContextKeys.SkillObservations (gated on the master's
    /// output_schema == observation) so DeliverFindings stops reporting 0.</summary>
    public const string CollectMasterFindings = "CollectMasterFindingsCommand";

    public const string DeliverFindings = "DeliverFindingsCommand";

    public const string CompressApiScanFindings = "CompressApiScanFindingsCommand";
}
