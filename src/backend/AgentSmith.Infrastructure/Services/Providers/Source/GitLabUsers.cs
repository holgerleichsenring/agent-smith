using System.Text.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-08-f147: what the GitLab users API says — who the token is (GET /user) and whether an
/// author is a bot (GET /users/:id), each read once per instance.
/// </summary>
public sealed class GitLabUsers(string baseUrl, string token, HttpClient http)
{
    private readonly Dictionary<long, bool> _bots = [];
    private long? _tokenUser;
    private bool _tokenUserRead;

    public async Task<long?> TokenUserIdAsync(CancellationToken ct)
    {
        if (_tokenUserRead) return _tokenUser;
        using var user = await GetAsync($"{baseUrl}/api/v4/user", ct);
        _tokenUserRead = true;
        return _tokenUser = user is not null && user.RootElement.ValueKind == JsonValueKind.Object
            && user.RootElement.TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.Number ? id.GetInt64() : null;
    }

    public async Task<bool> IsBotAsync(long userId, CancellationToken ct)
    {
        if (_bots.TryGetValue(userId, out var known)) return known;
        using var user = await GetAsync($"{baseUrl}/api/v4/users/{userId}", ct);
        return _bots[userId] = user is not null && user.RootElement.ValueKind == JsonValueKind.Object
            && user.RootElement.TryGetProperty("bot", out var bot) && bot.ValueKind == JsonValueKind.True;
    }

    private async Task<JsonDocument?> GetAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("PRIVATE-TOKEN", token);
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode
            ? await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct)
            : null;
    }
}
