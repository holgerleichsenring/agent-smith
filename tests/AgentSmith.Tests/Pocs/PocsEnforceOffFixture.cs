namespace AgentSmith.Tests.Pocs;

/// <summary>2026-11-23-933da: the same configuration on an installation without sign-in.</summary>
public sealed class PocsEnforceOffFixture : PocsAuthorityFixture
{
    protected override bool Enforce => false;
}
