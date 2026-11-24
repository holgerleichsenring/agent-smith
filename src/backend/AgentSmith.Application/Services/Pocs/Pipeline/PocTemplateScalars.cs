using System.Globalization;

namespace AgentSmith.Application.Services.Pocs.Pipeline;

/// <summary>2026-11-23-933dd: the values every host's YAML takes in a line — the marker, the job timeouts, the names the
/// push job uses and the Trivy image.</summary>
public static class PocTemplateScalars
{
    public static IReadOnlyDictionary<string, string> Of(PocPipelineValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var b = values.Build;
        return new Dictionary<string, string>
        {
            ["MARKER"] = PocPipelineTemplate.Marker + " — written by poc-target-install; install again rather than edit it",
            ["BUILD_MINUTES"] = b.BuildTimeoutMinutes.ToString(CultureInfo.InvariantCulture),
            ["SCAN_MINUTES"] = b.ScanTimeoutMinutes.ToString(CultureInfo.InvariantCulture),
            ["PUSH_MINUTES"] = b.PushTimeoutMinutes.ToString(CultureInfo.InvariantCulture),
            ["GITHUB_ENVIRONMENT"] = b.GithubEnvironment, ["GITLAB_ENVIRONMENT"] = b.GitlabEnvironment,
            ["PUSH_USER"] = b.PushUserSecret, ["PUSH_PASSWORD"] = b.PushPasswordSecret,
            ["AZURE_SERVICE_CONNECTION"] = b.AzureServiceConnection, ["TRIVY_IMAGE"] = values.TrivyImage,
        };
    }
}
