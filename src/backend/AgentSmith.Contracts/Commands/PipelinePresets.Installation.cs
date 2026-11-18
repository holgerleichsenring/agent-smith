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

    // The runs on the installation, launched by the server with a transcript or a request only it supplies.
    private static readonly Dictionary<string, IReadOnlyList<string>> InstallationSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        [AdminTurnName] = AdminTurn, [ConfigName] = Config,
    };

    private const string InstallationReason = "it runs on the installation itself with a conversation or a request "
        + "only the admin area supplies, so a ticket routed to it fails when it starts";

    /// <summary>Whether a preset runs on the installation rather than on a project.</summary>
    public static bool RunsOnInstallation(string pipelineName) => InstallationSteps.ContainsKey(pipelineName);
}
