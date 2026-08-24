using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// p0503d: the grant that makes lockout impossible. It unions with whatever the directory
/// already said, it reaches no editable surface, and every entry names the claim it is
/// matched against — a grant tried across claim types would make an attacker-controllable
/// claim colliding with a group identifier into an administrator.
/// </summary>
public sealed class AdminGrantTests
{
    private const string Subject = "8f1c9a30-0000-4000-8000-abcdefabcdef";

    [Fact]
    public void Roles_AdminGrant_IsAdminWithNoMappingAtAll()
    {
        var auth = new TokenAuthorityConfig();

        var identity = ResolverUnderTest.With(auth, $"sub:{Subject}")
            .Resolve(ResolverUnderTest.Caller(auth, ("sub", Subject)));

        identity.Roles.Should().Equal(BuiltInRoles.Admin);
        identity.Permissions.Should().Contain(Permissions.SecretsWrite);
    }

    [Fact]
    public void Roles_AdminGrant_UnionsWithRolesTheTokenAlreadyCarried()
    {
        var auth = new TokenAuthorityConfig
        {
            GroupRoles = new Dictionary<string, List<string>> { ["readers"] = [BuiltInRoles.Reader] },
        };

        var identity = ResolverUnderTest.With(auth, "group:platform-admins").Resolve(
            ResolverUnderTest.Caller(auth, ("groups", "readers"), ("groups", "/platform-admins")));

        identity.Roles.Should().BeEquivalentTo([BuiltInRoles.Reader, BuiltInRoles.Admin]);
    }

    [Fact]
    public void Roles_AdminGrant_IsReadThroughTheCapturedReader()
    {
        var asked = new List<string>();

        var grant = ResolverUnderTest.Grant($"sub:{Subject}", asked);

        asked.Should().ContainSingle("the delegate the composition root captured is the only "
            + "environment read — nothing here touches the process")
            .Which.Should().Be(AdminGrant.EnvVar);
        grant.Holds([], Subject).Should().BeTrue();
    }

    [Fact]
    public void Roles_AdminGrantWithoutAPrefix_IsRejectedRatherThanMatchedAcrossClaims()
    {
        var auth = new TokenAuthorityConfig();

        var identity = ResolverUnderTest.With(auth, Subject)
            .Resolve(ResolverUnderTest.Caller(auth, ("sub", Subject), ("groups", Subject)));

        identity.Roles.Should().BeEmpty();
        identity.Findings.Should().ContainSingle().Which.Should().Contain(Subject);
    }
}
