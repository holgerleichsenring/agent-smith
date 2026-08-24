using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503b: a real OIDC authority on loopback. JwtBearer resolves its signing keys through
/// discovery, so proving "a token is validated against one configured authority" needs an
/// authority that SERVES a discovery document and a key set — a fixture holding a symmetric
/// key would prove something else. It is infrastructure, and the tests treat it as such.
/// </summary>
public sealed class LocalTokenIssuer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly RsaSecurityKey _key;

    public string Authority { get; }

    private LocalTokenIssuer(WebApplication app, RsaSecurityKey key, string authority)
    {
        _app = app;
        _key = key;
        Authority = authority;
    }

    public static async Task<LocalTokenIssuer> StartAsync()
    {
        var key = new RsaSecurityKey(RSA.Create(2048)) { KeyId = "local-test-key" };
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");

        string? authority = null;
        app.MapGet("/.well-known/openid-configuration", () => Results.Json(new
        {
            issuer = authority,
            jwks_uri = $"{authority}/jwks",
            id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.RsaSha256 },
        }));
        app.MapGet("/jwks", () => Results.Json(KeySet(key)));

        await app.StartAsync();
        authority = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
        return new LocalTokenIssuer(app, key, authority);
    }

    /// <summary>A token this authority signed. Every argument is a way to make it wrong.</summary>
    public string Token(
        string? audience,
        IEnumerable<string>? permissions = null,
        string? issuer = null,
        TimeSpan? lifetime = null)
    {
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(10));
        var claims = (permissions ?? [])
            .Select(p => new Claim(PermissionClaimType, p))
            .Append(new Claim(JwtRegisteredClaimNames.Sub, "test-caller"))
            .ToList();

        var token = new JwtSecurityToken(
            issuer: issuer ?? Authority,
            audience: audience,
            claims: claims,
            notBefore: expires.AddMinutes(-30),
            expires: expires,
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>An already-expired token, for the case the lifetime check exists to catch.</summary>
    public string ExpiredToken(string? audience) => Token(
        audience, issuer: null, lifetime: TimeSpan.FromMinutes(-10));

    // Mirrors AgentSmith.Server.Security.PermissionClaims.Type, which is internal to the
    // server assembly and therefore not nameable from a token the test signs.
    private const string PermissionClaimType = "permission";

    private static object KeySet(RsaSecurityKey key)
    {
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        return new
        {
            keys = new[]
            {
                new { kty = "RSA", use = "sig", alg = SecurityAlgorithms.RsaSha256, kid = key.KeyId, n = jwk.N, e = jwk.E },
            },
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
