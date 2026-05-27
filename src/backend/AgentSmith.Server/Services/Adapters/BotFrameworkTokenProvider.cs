using System.Text.Json.Nodes;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Acquires and caches OAuth tokens for the Bot Framework REST API.
/// Tokens are refreshed 60 seconds before expiry.
/// </summary>
public sealed class BotFrameworkTokenProvider(
    HttpClient httpClient,
    TeamsAdapterOptions options)
{
    private const string TokenUrl =
        "https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token";

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
                return _cachedToken;

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = options.AppId,
                ["client_secret"] = options.AppPassword,
                ["scope"] = "https://api.botframework.com/.default",
            });

            using var response = await httpClient.PostAsync(TokenUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var json = JsonNode.Parse(body);

            _cachedToken = json?["access_token"]?.GetValue<string>()
                           ?? throw new InvalidOperationException("No access_token in response");
            var expiresIn = json?["expires_in"]?.GetValue<int>() ?? 3600;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

            return _cachedToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}
