using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933dd: how a PoC target's image is built — by a pipeline of its source repo's host, written there by
/// poc-target-install — from <c>pocs.targets[].build</c>; an absent block reads as these defaults. The names, the job
/// timeouts and the Trivy image are rendered into the pipeline at install (install again after changing them); the
/// pipeline name, queue_minutes, poll_seconds and report_kb are read by every build. The push credential lives only in
/// the host, named here, and only the push job can reach it.
/// </summary>
public sealed partial class PocBuildConfig
{
    public const string Key = "pocs.targets[].build";

    /// <summary>The Azure DevOps pipeline the operator creates from the YAML the install wrote (folder\name in a folder).</summary>
    public string Pipeline { get; set; } = "poc-build";

    /// <summary>GitHub: the environment holding the push secrets; only the push job names it.</summary>
    public string GithubEnvironment { get; set; } = "poc-push";

    /// <summary>GitLab: the environment the protected push variables are scoped to; only the push job declares it.</summary>
    public string GitlabEnvironment { get; set; } = "poc-push";

    /// <summary>GitHub secret / GitLab variable holding the registry push user.</summary>
    public string PushUserSecret { get; set; } = "POC_REGISTRY_USER";

    /// <summary>GitHub secret / GitLab variable holding the registry push password or token.</summary>
    public string PushPasswordSecret { get; set; } = "POC_REGISTRY_PASSWORD";

    /// <summary>Azure DevOps: the Docker registry service connection the push job alone uses.</summary>
    public string AzureServiceConnection { get; set; } = "poc-registry";

    public int QueueMinutes { get; set; } = 3;

    public int BuildTimeoutMinutes { get; set; } = 12;

    public int ScanTimeoutMinutes { get; set; } = 5;

    public int PushTimeoutMinutes { get; set; } = 3;

    public int PollSeconds { get; set; } = 15;

    /// <summary>The largest scan report read back, in KB.</summary>
    public int ReportKb { get; set; } = 1024;

    /// <summary>The longest one build may take, every job's timeout and the queue added: the step's deadline.</summary>
    public int DeadlineMinutes() => QueueMinutes + BuildTimeoutMinutes + ScanTimeoutMinutes + PushTimeoutMinutes;

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason.</summary>
    public IEnumerable<string> Problems(string target) => [.. NameProblems(target), .. NumberProblems(target)];

    private static string Field(string field, string target) => $"{Key}.{field} of target '{target}'";

    private IEnumerable<string> NameProblems(string target)
    {
        if (!PipelineName().IsMatch(Pipeline))
            yield return $"{Field("pipeline", target)} is '{Pipeline}'; it must be a pipeline name (letters, digits, ' ', '.', "
                + "'_', '-', '\\' for a folder), because Azure DevOps starts the build by it";
        foreach (var (field, value) in new[] { ("github_environment", GithubEnvironment), ("gitlab_environment", GitlabEnvironment),
            ("azure_service_connection", AzureServiceConnection) }.Where(f => !PlainName().IsMatch(f.Item2)))
            yield return $"{Field(field, target)} is '{value}'; it must be a name of letters, digits, '.', '_' and '-', because it "
                + "is written into the build pipeline";
        foreach (var (field, value) in new[] { ("push_user_secret", PushUserSecret), ("push_password_secret", PushPasswordSecret) }
            .Where(f => !VariableName().IsMatch(f.Item2)))
            yield return $"{Field(field, target)} is '{value}'; it must be a variable name (letters, digits and '_', not starting "
                + "with a digit), because the push job reads the credential by it";
    }

    private IEnumerable<string> NumberProblems(string target)
    {
        foreach (var (field, value) in new[] { ("queue_minutes", QueueMinutes), ("build_timeout_minutes", BuildTimeoutMinutes),
            ("scan_timeout_minutes", ScanTimeoutMinutes), ("push_timeout_minutes", PushTimeoutMinutes),
            ("poll_seconds", PollSeconds), ("report_kb", ReportKb) }.Where(f => f.Item2 < 1))
            yield return $"{Field(field, target)} is {value}; it must be 1 or more, because 0 or less would fail every build";
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9 ._\\-]{0,199}$")]
    private static partial Regex PipelineName();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$")]
    private static partial Regex PlainName();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,99}$")]
    private static partial Regex VariableName();
}
