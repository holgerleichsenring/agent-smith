using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-9992h: a GitHub App machine identity's tokens, as GitHub documents them. The App authenticates with a JWT signed
/// RS256 by its private key — <c>iss</c> the app id (or client id), <c>iat</c> 60 s in the past, <c>exp</c> at most ten minutes
/// on — and trades it at <c>POST /app/installations/{id}/access_tokens</c> (201) for an installation token valid one hour; its
/// own login is <c>{slug}[bot]</c>, from <c>GET /app</c>. Tokens are kept per host, app and installation until five minutes
/// before <c>expires_at</c>; one installation mints one at a time. Synchronous (<see cref="HttpClient.Send(HttpRequestMessage)"/>),
/// because the credential resolver is; a refusal is a <see cref="MachineCredentialException"/> with GitHub's answer.
/// </summary>
public sealed class GitHubAppTokens(HttpClient http, TimeProvider clock)
{
    private const string ApiVersion = "2022-11-28";
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5); // not a limit: renews ahead of GitHub's own expiry, which decides
    private readonly ConcurrentDictionary<string, (string Token, DateTimeOffset Until)> _tokens = new();
    private readonly ConcurrentDictionary<string, string> _logins = new();
    private readonly ConcurrentDictionary<string, object> _gates = new();

    public string InstallationToken(MachineTarget target, MachineIdentity identity, string privateKeyPem) =>
        Refusing(target, identity, () => Mint(target, identity, privateKeyPem));

    public string AppLogin(MachineTarget target, MachineIdentity identity, string privateKeyPem) =>
        Refusing(target, identity, () => Login(target, identity, privateKeyPem));

    private string Mint(MachineTarget target, MachineIdentity identity, string privateKeyPem)
    {
        var (api, appId, installation) = Target(target, identity);
        var key = $"{api}|{appId}|{installation}";
        lock (_gates.GetOrAdd(key, _ => new object()))
        {
            if (_tokens.TryGetValue(key, out var kept) && kept.Until > clock.GetUtcNow()) return kept.Token;
            using var answer = Send(HttpMethod.Post, $"{api}/app/installations/{Uri.EscapeDataString(installation)}/access_tokens",
                Jwt(appId, privateKeyPem));
            var body = Body(answer, HttpStatusCode.Created, target, identity);
            var token = body.GetProperty("token").GetString() ?? throw Refused(target, identity, "GitHub answered no token");
            var until = body.GetProperty("expires_at").GetDateTimeOffset() - Margin;
            _tokens[key] = (token, until);
            return token;
        }
    }

    private string Login(MachineTarget target, MachineIdentity identity, string privateKeyPem)
    {
        var (api, appId, _) = Target(target, identity);
        return _logins.GetOrAdd($"{api}|{appId}", _ =>
        {
            using var answer = Send(HttpMethod.Get, $"{api}/app", Jwt(appId, privateKeyPem));
            var slug = Body(answer, HttpStatusCode.OK, target, identity).GetProperty("slug").GetString();
            return string.IsNullOrWhiteSpace(slug) ? throw Refused(target, identity, "GitHub named no slug for the App") : $"{slug}[bot]";
        });
    }

    /// <summary>The App's JWT: RS256 over <c>iat</c> (60 s back, against clock drift), <c>exp</c> (nine minutes on) and <c>iss</c>.</summary>
    internal string Jwt(string appId, string privateKeyPem)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { iat = now - 60, exp = now + 540, iss = appId }));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    // A bad key, an unreadable answer, a refused connection or the timeout are the identity's problem, said by name like a
    // refusal — never an exception the acts do not expect, and never a cancellation nobody asked for.
    private static string Refusing(MachineTarget target, MachineIdentity identity, Func<string> act)
    {
        try { return act(); }
        catch (MachineCredentialException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException
            or InvalidOperationException or ArgumentException or CryptographicException or FormatException)
        {
            throw Refused(target, identity, ex is TaskCanceledException ? "GitHub did not answer in time" : ex.Message);
        }
    }

    private static (string Api, string AppId, string Installation) Target(MachineTarget target, MachineIdentity identity) =>
        (GitHubApiEndpoints.For(target.Url).RestRoot, identity.AppId!, identity.InstallationId ?? string.Empty);

    private HttpResponseMessage Send(HttpMethod method, string url, string jwt)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("agent-smith", "1.0"));
        return http.Send(request);
    }

    private static JsonElement Body(HttpResponseMessage answer, HttpStatusCode expected, MachineTarget target, MachineIdentity identity)
    {
        using var reader = new StreamReader(answer.Content.ReadAsStream());
        var text = reader.ReadToEnd();
        if (answer.StatusCode != expected)
            throw Refused(target, identity, $"GitHub answered {(int)answer.StatusCode}: {(text.Length > 300 ? text[..300] : text)}");
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static MachineCredentialException Refused(MachineTarget target, MachineIdentity identity, string answer) =>
        new(target.Entity, identity.Auth, answer);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
