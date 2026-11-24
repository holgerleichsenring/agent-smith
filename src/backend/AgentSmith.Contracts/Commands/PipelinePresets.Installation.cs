namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>2026-11-17-f3a8a: preset name of one admin assistant turn, run on the installation.</summary>
    public const string AdminTurnName = "admin-turn";

    /// <summary>The master an admin turn loads — embedded in the server, because its tools are the server's own.</summary>
    public const string AdminAssistantMaster = "admin-assistant-master";

    public static readonly IReadOnlyList<string> AdminTurn =
        [CommandNames.AdminAssistantTurn, CommandNames.CollectSpecDialogReply];

    /// <summary>2026-11-17-f3a8c: preset name of a config run — an applied set of configuration changes.</summary>
    public const string ConfigName = "config";

    public static readonly IReadOnlyList<string> Config = [CommandNames.ApplyConfigChanges];

    /// <summary>2026-11-17-f3a8d: preset name of a secret run — a value set, deleted or re-wrapped in the admin area.</summary>
    public const string SecretName = "secret";

    public static readonly IReadOnlyList<string> Secret = [CommandNames.WriteSecret];

    /// <summary>2026-11-23-933dd: the run on the installation that writes a PoC target's build pipeline definition into
    /// its source repo, started by a pocs.admin holder from the PoC settings. Declared here, above the dictionary that
    /// reads it, because field initialisers run in text order only within one file.</summary>
    public const string PocTargetInstallName = "poc-target-install";

    public static readonly IReadOnlyList<string> PocTargetInstall = [CommandNames.LoadCatalog, CommandNames.PocTargetInstall];

    /// <summary>2026-11-23-c81ee: the run on the installation that deletes the stored PoC uploads older than
    /// pocs.blocked_retention_days and what deleted PoCs still hold of their variables, started at most daily by the
    /// lifetime pass. Declared above the dictionary for the same reason.</summary>
    public const string PocUploadPurgeName = "poc-upload-purge";

    public static readonly IReadOnlyList<string> PocUploadPurge = [CommandNames.PurgePocUploads];

    // The runs on the installation, launched by the server with a transcript or a request only it supplies.
    private static readonly Dictionary<string, IReadOnlyList<string>> InstallationSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        [AdminTurnName] = AdminTurn, [ConfigName] = Config, [SecretName] = Secret,
        [PocTargetInstallName] = PocTargetInstall, [PocUploadPurgeName] = PocUploadPurge, // 2026-11-23-933dd, c81ee
    };

    private const string InstallationReason = "it runs on the installation itself with a conversation or a request "
        + "only the admin area supplies, so a ticket routed to it fails when it starts";

    /// <summary>Whether a preset runs on the installation rather than on a project.</summary>
    public static bool RunsOnInstallation(string pipelineName) => InstallationSteps.ContainsKey(pipelineName);
}
