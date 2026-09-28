using AgentSmith.Server.Security;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-09-27-481bd: whether the claim an installation names its callers by is one a person
/// recognises.
/// <para>
/// The identity's subject carries the NAME-CLAIM value and falls back to the opaque one only when
/// the directory sent none — so a name is always present and is not always a name. A page that
/// greets somebody must know which it has; a directory identifier is worse than no name at all.
/// </para>
/// </summary>
public sealed class ReadableNameTests
{
    [Fact]
    public void Identity_AnInstallationNamingAReadableClaim_SaysTheNameIsNotOpaque()
    {
        ReadableName.Is("preferred_username").Should().BeTrue();
        ReadableName.Is("upn").Should().BeTrue();
    }

    [Fact]
    public void Identity_AnInstallationOnTheDefaultClaim_SaysTheNameIsOpaque()
    {
        ReadableName.Is("sub").Should().BeFalse();
    }

    [Fact]
    public void Identity_TheRule_DetectsTheDefaultAndNotOpacityItself()
    {
        // Stated rather than implied: an installation pointing the claim at an object id passes
        // this test and gets greeted by it. Claiming more would be a promise about somebody else's
        // directory, and the default is what most installations never change.
        ReadableName.Is("oid").Should().BeTrue();
    }
}
