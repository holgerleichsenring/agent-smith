using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-d0e2: a Microsoft Entra service principal's access tokens for Azure DevOps, as Microsoft documents them: the client
/// credentials grant at <c>POST https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token</c> with the client id, the client
/// secret and the Azure DevOps scope <c>499b84ac-1321-427f-aa17-267ca6975798/.default</c>; the answer's <c>access_token</c> is
/// kept per tenant and client for half of its <c>expires_in</c>, because the clients and a CI wait hold the token they were
/// given for a while, and one principal mints one at a time.
/// Synchronous, as <see cref="GitHubAppTokens"/> is; a refusal is a <see cref="MachineCredentialException"/> with Entra's answer.
/// </summary>
public sealed class EntraPrincipalTokens(HttpClient http, TimeProvider clock)
{
    internal const string Authority = "https://login.microsoftonline.com";
    internal const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Until)> _tokens = new();
    private readonly ConcurrentDictionary<string, object> _gates = new();

    public string Token(MachineTarget target, MachineIdentity identity, string clientSecret)
    {
        try { return Mint(target, identity, clientSecret); }
        catch (MachineCredentialException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException
            or InvalidOperationException or ArgumentException or FormatException)
        {
            throw Refused(target, identity, ex is TaskCanceledException ? "Entra did not answer in time" : ex.Message);
        }
    }

    private string Mint(MachineTarget target, MachineIdentity identity, string clientSecret)
    {
        var key = $"{identity.TenantId}|{identity.ClientId}";
        lock (_gates.GetOrAdd(key, _ => new object()))
        {
            if (_tokens.TryGetValue(key, out var kept) && kept.Until > clock.GetUtcNow()) return kept.Token;
            using var answer = http.Send(Request(identity, clientSecret));
            var body = Body(answer, target, identity);
            var token = body.GetProperty("access_token").GetString() ?? throw Refused(target, identity, "Entra answered no access_token");
            // not a limit: renewed at half its life, so a token handed out stays good for at least that half; Entra's expiry decides
            _tokens[key] = (token, clock.GetUtcNow().AddSeconds(body.GetProperty("expires_in").GetDouble() / 2));
            return token;
        }
    }

    private static HttpRequestMessage Request(MachineIdentity identity, string clientSecret) =>
        new(HttpMethod.Post, $"{Authority}/{Uri.EscapeDataString(identity.TenantId!)}/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials", ["client_id"] = identity.ClientId!,
                ["client_secret"] = clientSecret, ["scope"] = AzureDevOpsScope,
            }),
        };

    private static JsonElement Body(HttpResponseMessage answer, MachineTarget target, MachineIdentity identity)
    {
        using var reader = new StreamReader(answer.Content.ReadAsStream());
        var text = reader.ReadToEnd();
        if (answer.StatusCode == HttpStatusCode.OK) return JsonDocument.Parse(text).RootElement.Clone();
        throw Refused(target, identity, $"Entra answered {(int)answer.StatusCode}: {Said(text)}");
    }

    // Entra names the cause in error_description (AADSTS…); an answer without one is quoted, cut to stay readable.
    private static string Said(string text)
    {
        try
        {
            using var answer = JsonDocument.Parse(text);
            if (answer.RootElement.TryGetProperty("error_description", out var said) && said.GetString() is { Length: > 0 } d)
                return d.Length > 300 ? d[..300] : d;
        }
        catch (JsonException) { }
        return text.Length > 300 ? text[..300] : text;
    }

    private static MachineCredentialException Refused(MachineTarget target, MachineIdentity identity, string answer) =>
        new(target.Entity, identity.Auth, answer);
}
