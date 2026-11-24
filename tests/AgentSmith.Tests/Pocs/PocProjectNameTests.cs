using System.Security.Claims;
using AgentSmith.Server.Services.Pocs.Import;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933db: a person's PoC project key comes from a READABLE claim, never a slugged GUID; with none it
/// is 'u' plus a hash; a key someone else holds takes a hash suffix that is the same for the same person.</summary>
public sealed class PocProjectNameTests
{
    [Fact]
    public void Base_SubNameClaim_TakesPreferredUsername()
    {
        PocProjectName.Base(User("0f3c-9a", ("preferred_username", "ann.lee")), "sub").Should().Be("poc-ann-lee");
    }

    [Fact]
    public void Base_OnlyAnEmail_TakesItsLocalPart()
    {
        PocProjectName.Base(User("0f3c-9a", ("email", "Bob.Smith@example.com")), "sub").Should().Be("poc-bob-smith");
    }

    [Fact]
    public void Base_AReadableNameClaim_IsUsedFirst()
    {
        PocProjectName.Base(User("Eve Example", ("preferred_username", "other")), "name").Should().Be("poc-eve-example");
    }

    [Fact]
    public void Base_NoReadableClaim_IsUPlusEightHex_TheSameTwice()
    {
        var key = PocProjectName.Base(User("0f3c-9a"), "sub");

        key.Should().MatchRegex("^poc-u[0-9a-f]{8}$");
        PocProjectName.Base(User("0f3c-9a"), "sub").Should().Be(key);
    }

    [Fact]
    public void Base_ALongName_StaysADnsLabelWithRoomForTheSuffix()
    {
        var key = PocProjectName.Base(User("x", ("preferred_username", new string('a', 80))), "sub");

        key.Length.Should().BeLessThanOrEqualTo(58);
        PocProjectName.Suffixed(key, User("x")).Length.Should().BeLessThanOrEqualTo(63);
    }

    [Fact]
    public void Suffixed_AppendsFourHex_SameForTheSamePerson_DifferentForAnother()
    {
        var ann = PocProjectName.Suffixed("poc-ann", User("ann-sub"));

        ann.Should().MatchRegex("^poc-ann-[0-9a-f]{4}$");
        PocProjectName.Suffixed("poc-ann", User("ann-sub")).Should().Be(ann);
        PocProjectName.Suffixed("poc-ann", User("other-sub")).Should().NotBe(ann);
    }

    [Fact]
    public void NameValue_IsTheIdentitysName()
    {
        PocProjectName.NameValue(User("ann-sub")).Should().Be("ann-sub");
    }

    internal static ClaimsPrincipal User(string name, params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity([new Claim("sub", name), new Claim(ClaimTypes.Name, name), .. claims.Select(c => new Claim(c.Type, c.Value))], "test"));
}
