using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933da: one PoC target — the shared source repo every poc- project's folders live in, the folders the
/// later slices write, the host pattern, and the lifetime and per-person rules. A PoC's source folder is
/// &lt;root&gt;/&lt;project&gt;/&lt;poc&gt;/.
/// </summary>
public sealed partial class PocTargetConfig
{
    /// <summary>A DNS label: it becomes part of names on the target.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One exact project repo ref: a repos-catalog name or connection/name. A glob is refused.</summary>
    public string SourceRepo { get; set; } = string.Empty;

    public string Root { get; set; } = "pocs";

    /// <summary>Where c81eb writes &lt;deploy_root&gt;/&lt;poc&gt;/.</summary>
    public string DeployRoot { get; set; } = "deploy";

    /// <summary>Where 933dd and c81eb write the app template.</summary>
    public string TemplatePath { get; set; } = "poc-template";

    /// <summary>A host pattern with exactly one {app}, e.g. '{app}.poc.example.internal'. No default.</summary>
    public string? DomainPattern { get; set; }

    public int LifetimeDays { get; set; } = 90;

    public List<int> ReminderDays { get; set; } = [14, 7, 1];

    /// <summary>The most live PoCs one person holds on this target; pocs.admin is not bound by it.</summary>
    public int PerPersonLimit { get; set; } = 3;

    /// <summary>2026-11-23-933de: the container registry PoC images are read from. Required.</summary>
    public PocRegistryConfig? Registry { get; set; }

    /// <summary>2026-11-23-933dd: how the source repo host's pipeline builds, scans and pushes the image.</summary>
    public PocBuildConfig Build { get; set; } = new();

    /// <summary>2026-11-23-933dg: how the PoCs' secret variables and pull secret reach the target.</summary>
    public PocSecretsConfig Secrets { get; set; } = new();

    /// <summary>2026-11-23-c81eb: how its PoCs are reached, put behind auth and confined.</summary>
    public PocIngressConfig Ingress { get; set; } = new();

    public PocAuthConfig Auth { get; set; } = new();

    public PocNetworkConfig Network { get; set; } = new();

    public PocQuotaConfig Quota { get; set; } = new();

    public PocContainerConfig Container { get; set; } = new();

    /// <summary>This target's value problems, one sentence each naming the key, the value and the reason;
    /// 2026-11-23-c81eb: with <paramref name="pocsEnabled"/> the deploy keys a PoC cannot run without are required.</summary>
    public IEnumerable<string> Problems(bool pocsEnabled = false) =>
        [.. NameProblems(), .. SourceRepoProblems(), .. new PocTargetFolders(this).Problems(), .. DomainProblems(),
            .. LifetimeProblems(), .. RegistryProblems(), .. Build.Problems(Name),
            .. Secrets.Problems(Name), .. new PocTargetExposure(this).Problems(pocsEnabled), .. new PocTargetResources(this).Problems()];

    private string Key(string field) => $"pocs.targets[].{field} of target '{Name}'";

    private IEnumerable<string> NameProblems()
    {
        if (!DnsLabel().IsMatch(Name))
            yield return $"pocs.targets[].name is '{Name}'; it must be a DNS label (lower-case letters, digits and '-', at "
                + "most 63), because it becomes part of names on the target";
    }

    private IEnumerable<string> SourceRepoProblems()
    {
        if (string.IsNullOrWhiteSpace(SourceRepo))
            yield return $"{Key("source_repo")} is empty; name the repository PoC sources are committed to";
        else if (SourceRepo.Contains('*'))
            yield return $"{Key("source_repo")} is '{SourceRepo}'; it must name one repository exactly, because a glob "
                + "would bind every poc- project to many repositories";
    }

    private IEnumerable<string> DomainProblems()
    {
        if (string.IsNullOrWhiteSpace(DomainPattern)) yield break;
        var apps = DomainPattern.Split("{app}").Length - 1;
        if (apps != 1 || !HostName().IsMatch(DomainPattern.Replace("{app}", "app", StringComparison.Ordinal)))
            yield return $"{Key("domain_pattern")} is '{DomainPattern}'; it must be a host name with exactly one {{app}}, "
                + "because each PoC's host is made from it";
    }

    private IEnumerable<string> LifetimeProblems()
    {
        if (LifetimeDays < 1)
            yield return $"{Key("lifetime_days")} is {LifetimeDays}; it must be 1 or more, because a PoC lives that many days";
        foreach (var day in ReminderDays.Where(d => d < 1 || d >= LifetimeDays))
            yield return $"{Key("reminder_days")} holds {day}; each day must be 1 or more and below lifetime_days "
                + $"({LifetimeDays}), because a reminder is sent that many days before a PoC expires";
        if (PerPersonLimit < 1)
            yield return $"{Key("per_person_limit")} is {PerPersonLimit}; it must be 1 or more, because 0 would let nobody "
                + "but an admin import a PoC";
    }

    private IEnumerable<string> RegistryProblems() => Registry is null
        ? [$"{Key("registry")} is missing; name the container registry, because every PoC image is resolved and verified there"]
        : Registry.Problems().Select(problem => $"{problem} (target '{Name}')");

    [GeneratedRegex("^[a-z0-9]([-a-z0-9]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsLabel();

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9]([-a-z0-9]{0,61}[a-z0-9])?)(\.[a-z0-9]([-a-z0-9]{0,61}[a-z0-9])?)+$")]
    private static partial Regex HostName();
}
