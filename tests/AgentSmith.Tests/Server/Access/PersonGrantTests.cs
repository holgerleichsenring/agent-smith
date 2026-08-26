using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using AgentSmith.Tests.Server.Auth;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Server.Access;

/// <summary>
/// 2026-08-26-7a51: a role granted to a PERSON — stored against the claim it was written
/// for, additive beside whatever the directory says, and applying on the next request.
/// </summary>
public sealed class PersonGrantTests
{
    private static readonly TokenAuthorityConfig Named =
        new() { NameClaim = "preferred_username", RoleClaim = "roles", GroupClaim = "groups" };

    [Fact]
    public void Grant_ToAPerson_ResolvesOnTheNextRequestWithoutARestart()
    {
        using var h = new AccessTestHarness(auth: Named);
        var caller = ResolverUnderTest.Caller(Named, ("preferred_username", "ada@example.com"));
        var resolver = h.Resolver();
        resolver.Resolve(caller).Roles.Should().BeEmpty();

        h.Writer.Save(Mapping(Grant("preferred_username", "ada@example.com", "operator")), Actor);

        resolver.Resolve(caller).Roles.Should().Contain("operator");
    }

    [Fact]
    public void Grant_WrittenAgainstAnotherClaim_ResolvesNothingAndIsReported()
    {
        using var h = new AccessTestHarness(auth: Named);
        h.Writer.Save(Mapping(Grant("email", "ada@example.com", "admin")), Actor);

        var identity = h.Resolver().Resolve(
            ResolverUnderTest.Caller(Named, ("preferred_username", "ada@example.com"), ("email", "ada@example.com")));

        identity.Roles.Should().BeEmpty("a grant matched across claim types is a way in nobody intended");
        identity.Findings.Should().ContainMatch("*written against the claim 'email'*");
    }

    [Fact]
    public void Grant_AndADirectoryRole_AreBothHeld()
    {
        using var h = new AccessTestHarness(auth: Named);
        h.Writer.Save(Mapping(Grant("preferred_username", "ada@example.com", "admin")), Actor);

        var identity = h.Resolver().Resolve(ResolverUnderTest.Caller(
            Named, ("preferred_username", "ada@example.com"), ("roles", "reader")));

        identity.Roles.Should().BeEquivalentTo(["admin", "reader"]);
    }

    [Fact]
    public void Grant_ToSomeoneNeverSeen_AppliesOnTheirFirstRequest()
    {
        using var h = new AccessTestHarness(auth: Named);
        h.Writer.Save(Mapping(Grant("preferred_username", "newcomer@example.com", "reader")), Actor);

        h.Resolver().Resolve(ResolverUnderTest.Caller(Named, ("preferred_username", "newcomer@example.com")))
            .Roles.Should().Contain("reader", "a grant is a decision, not a record of a visit");
    }

    [Fact]
    public void Grant_DifferingOnlyInCase_DoesNotMatch()
    {
        using var h = new AccessTestHarness(auth: Named);
        h.Writer.Save(Mapping(Grant("preferred_username", "Ada@example.com", "admin")), Actor);

        h.Resolver().Resolve(ResolverUnderTest.Caller(Named, ("preferred_username", "ada@example.com")))
            .Roles.Should().BeEmpty("a name-claim value is an identifier, and identifiers compare ordinally");
    }

    [Fact]
    public void GroupRoles_KeyWithALeadingSlash_MatchesTheUnprefixedValue()
    {
        using var h = new AccessTestHarness(auth: Named);
        var mapping = Mapping();
        mapping.GroupRoles["/platform-admins"] = ["admin"];
        h.Writer.Save(mapping, Actor);

        h.Resolver().Resolve(ResolverUnderTest.Caller(
            Named, ("preferred_username", "ada@example.com"), ("groups", "platform-admins")))
            .Roles.Should().Contain("admin");
    }

    private static ChangeAttribution Actor => new("tester");

    private static PersonGrant Grant(string claim, string value, params string[] roles) =>
        new() { Claim = claim, Value = value, Roles = [.. roles] };

    private static RoleMappingConfig Mapping(params PersonGrant[] grants) => new()
    {
        RoleClaim = "roles", GroupClaim = "groups", PersonGrants = [.. grants],
    };
}
