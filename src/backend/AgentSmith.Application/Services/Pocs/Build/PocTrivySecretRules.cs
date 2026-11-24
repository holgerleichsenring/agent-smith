using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.Build;

/// <summary>
/// 2026-11-23-933dd: Trivy's built-in secret rules carry no confidence, so each one that has a counterpart in the skills
/// catalog's 'secrets' patterns is mapped to it and takes its confidence — the build passes the mapped rules whose
/// pattern reaches pocs.scan.secret_min_confidence as secret_rules, so both stages block the same secrets. No rule maps
/// to a publishable key. An unmapped rule never blocks.
/// </summary>
public sealed class PocTrivySecretRules(IPocSecretConfidences confidences)
{
    private static readonly Dictionary<string, string> CatalogPattern = new(StringComparer.Ordinal)
    {
        ["aws-access-key-id"] = "aws-access-key", ["aws-secret-access-key"] = "aws-secret-key",
        ["github-pat"] = "github-pat", ["github-fine-grained-pat"] = "github-pat", ["github-oauth"] = "github-oauth",
        ["github-app-token"] = "github-app", ["github-refresh-token"] = "github-app",
        ["stripe-secret-token"] = "stripe-secret-key", ["private-key"] = "rsa-private-key",
        ["slack-access-token"] = "slack-token", ["slack-web-hook"] = "slack-webhook", ["gcp-api-key"] = "google-api-key",
        ["twilio-api-key"] = "twilio-api-key", ["sendgrid-api-token"] = "sendgrid-api-key", ["npm-access-token"] = "npm-token",
        ["pypi-upload-token"] = "pypi-token", ["mailchimp-api-key"] = "mailchimp-api-key", ["jwt-token"] = "jwt-token",
    };

    /// <summary>The catalog confidence of a Trivy rule, null when it maps to no catalog pattern; a failure when the
    /// catalog's secret patterns could not be read.</summary>
    public (Func<string, int?> ConfidenceOf, string? Failure) Read()
    {
        var (byPattern, failure) = confidences.Read();
        if (failure is not null) return (_ => null, failure);
        return (rule => CatalogPattern.TryGetValue(rule, out var pattern) && byPattern.TryGetValue(pattern, out var c) ? c : null, null);
    }

    /// <summary>The Trivy rule ids that block at <paramref name="minConfidence"/>, sorted.</summary>
    public (IReadOnlyList<string> Rules, string? Failure) Blocking(int minConfidence)
    {
        var (confidenceOf, failure) = Read();
        if (failure is not null) return ([], failure);
        return ([.. CatalogPattern.Keys.Where(rule => confidenceOf(rule) >= minConfidence).Order(StringComparer.Ordinal)], null);
    }
}
