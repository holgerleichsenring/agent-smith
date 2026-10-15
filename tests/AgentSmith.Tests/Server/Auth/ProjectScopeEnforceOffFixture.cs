namespace AgentSmith.Tests.Server.Auth;

/// <summary>2026-10-15-4a4db: the same bindings and projects with the enforce switch off.</summary>
public sealed class ProjectScopeEnforceOffFixture : ProjectScopeAuthorityFixture
{
    protected override bool Enforce => false;
}
