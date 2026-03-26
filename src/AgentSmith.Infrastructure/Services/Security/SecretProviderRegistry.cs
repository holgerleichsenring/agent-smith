using AgentSmith.Contracts.Models;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Maps secret pattern IDs to their cloud provider names and revocation URLs.
/// </summary>
public static class SecretProviderRegistry
{
    private static readonly Dictionary<string, SecretProvider> Providers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aws-access-key"] = new("AWS", "https://console.aws.amazon.com/iam/home#/security_credentials"),
        ["github-token"] = new("GitHub", "https://github.com/settings/tokens"),
        ["github-oauth"] = new("GitHub", "https://github.com/settings/tokens"),
        ["stripe-live-key"] = new("Stripe", "https://dashboard.stripe.com/apikeys"),
        ["stripe-restricted"] = new("Stripe", "https://dashboard.stripe.com/apikeys"),
        ["slack-token"] = new("Slack", "https://api.slack.com/apps"),
        ["slack-webhook"] = new("Slack", "https://api.slack.com/apps"),
        ["discord-token"] = new("Discord", "https://discord.com/developers/applications"),
        ["twilio-api-key"] = new("Twilio", "https://console.twilio.com/us1/account/keys-credentials/api-keys"),
        ["sendgrid-api-key"] = new("SendGrid", "https://app.sendgrid.com/settings/api_keys"),
        ["openai-api-key"] = new("OpenAI", "https://platform.openai.com/api-keys"),
        ["google-api-key"] = new("Google Cloud", "https://console.cloud.google.com/apis/credentials"),
        ["npm-token"] = new("npm", "https://www.npmjs.com/settings/~/tokens"),
        ["pypi-token"] = new("PyPI", "https://pypi.org/manage/account/#api-tokens"),
        ["mailchimp-api-key"] = new("Mailchimp", "https://us1.admin.mailchimp.com/account/api/"),
        ["generic-connection-string"] = new("Database", ""),
        ["private-key-rsa"] = new("SSH/TLS", ""),
        ["private-key-generic"] = new("SSH/TLS", ""),
    };

    /// <summary>
    /// Looks up the provider for a given pattern ID.
    /// Returns null if the pattern is not associated with a known provider.
    /// </summary>
    public static SecretProvider? Lookup(string patternId) =>
        Providers.GetValueOrDefault(patternId);
}
