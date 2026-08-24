using System.Security.Claims;
using System.Security.Cryptography;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: a directory that could not fit the caller's groups into the token says so, and
/// the server repeats it rather than reporting an unmapped group. The markers are minted
/// through the SAME handler JwtBearer uses, because <c>_claim_names</c> and
/// <c>_claim_sources</c> are JSON OBJECTS: they arrive as one claim whose value is JSON
/// text, and a test that minted a flat string would be green against a shape the directory
/// never sends.
/// </summary>
public sealed class GroupOverageTests
{
    private static readonly SymmetricSecurityKey Key =
        new(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Overage_ClaimNamesObjectShape_IsReportedAsItself()
    {
        var auth = new TokenAuthorityConfig();
        var caller = Minted(auth, new Dictionary<string, object>
        {
            ["_claim_names"] = new Dictionary<string, string> { ["groups"] = "src1" },
            ["_claim_sources"] = new Dictionary<string, object>
            {
                ["src1"] = new Dictionary<string, string> { ["endpoint"] = "https://a-directory/graph" },
            },
        });

        caller.FindFirst("_claim_names")!.Value.Should().StartWith("{",
            "the object arrives as one claim whose value is JSON text");
        var identity = ResolverUnderTest.With(auth).Resolve(caller);

        identity.GroupClaimValues.Should().BeEmpty();
        identity.Findings.Should().HaveCount(2);
        identity.Findings.Should().OnlyContain(finding => finding.Contains("too many groups"));
    }

    [Fact]
    public void Overage_HasGroupsMarker_IsReportedAsItself()
    {
        var auth = new TokenAuthorityConfig();

        var identity = ResolverUnderTest.With(auth).Resolve(
            Minted(auth, new Dictionary<string, object> { ["hasgroups"] = true }));

        identity.Findings.Should().ContainSingle().Which.Should().Contain("hasgroups");
    }

    private static ClaimsPrincipal Minted(TokenAuthorityConfig auth, Dictionary<string, object> claims)
    {
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "https://an-authority-under-test",
            Claims = claims,
            SigningCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256),
        });
        return new ClaimsPrincipal(new ClaimsIdentity(
            new JsonWebToken(token).Claims, "Bearer", auth.NameClaim, ClaimTypes.Role));
    }
}
