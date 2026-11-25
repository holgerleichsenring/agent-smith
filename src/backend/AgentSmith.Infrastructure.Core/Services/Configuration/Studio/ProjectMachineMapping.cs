using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>2026-11-25-9992h: the project's <c>machine:</c> block between the stored entry and the studio entity.</summary>
internal static class ProjectMachineMapping
{
    /// <summary>Null when the project declares no block.</summary>
    public static ProjectMachine? ToEntity(MachineIdentityConfig? machine) =>
        machine is null ? null : new ProjectMachine(machine.Name, machine.Email,
            Hoster(machine.Github), Hoster(machine.Gitlab), Hoster(machine.AzureDevops));

    /// <summary>The stored block for a sent one; one with no name, no email and no hoster clears it.</summary>
    public static MachineIdentityConfig? ToRaw(ProjectMachine machine)
    {
        ArgumentNullException.ThrowIfNull(machine);
        var raw = new MachineIdentityConfig
        {
            Name = machine.Name.Trim(), Email = machine.Email.Trim(),
            Github = Raw(machine.Github), Gitlab = Raw(machine.Gitlab), AzureDevops = Raw(machine.AzureDevops),
        };
        return raw is { Name.Length: 0, Email.Length: 0, Github: null, Gitlab: null, AzureDevops: null } ? null : raw;
    }

    private static ProjectMachineHoster? Hoster(MachineHosterConfig? entry) =>
        entry is null ? null : new ProjectMachineHoster(entry.Auth, entry.AppId, entry.InstallationId, entry.Email);

    private static MachineHosterConfig? Raw(ProjectMachineHoster? entry) => entry is null ? null : new MachineHosterConfig
    {
        Auth = entry.Auth.Trim(), AppId = Blank(entry.AppId), InstallationId = Blank(entry.InstallationId), Email = Blank(entry.Email),
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
