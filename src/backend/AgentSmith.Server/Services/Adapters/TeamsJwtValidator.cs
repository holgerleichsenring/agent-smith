using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Validates JWT tokens from the Microsoft Bot Framework.
/// Uses Microsoft's OpenID Connect metadata endpoint for signing key discovery.
/// </summary>
public sealed class TeamsJwtValidator
{
    private const string OpenIdMetadataUrl =
        "https://login.botframework.com/v1/.well-known/openidconfiguration";

    private const string BotFrameworkIssuer = "https://api.botframework.com";

    private readonly string _appId;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public TeamsJwtValidator(string appId)
    {
        _appId = appId;
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            OpenIdMetadataUrl,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    public async Task<bool> ValidateAsync(string authorizationHeader, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader))
            return false;

        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            var config = await _configManager.GetConfigurationAsync(cancellationToken);

            var validationParameters = new TokenValidationParameters
            {
                ValidIssuers = new[] { BotFrameworkIssuer },
                ValidAudiences = new[] { _appId },
                IssuerSigningKeys = config.SigningKeys,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
            };

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch (SecurityTokenException)
        {
            return false;
        }
    }
}
