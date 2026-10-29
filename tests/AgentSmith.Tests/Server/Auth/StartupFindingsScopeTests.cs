using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using static AgentSmith.Tests.Server.Auth.ProjectBindingTests;

namespace AgentSmith.Tests.Server.Auth;

/// <summary>
/// 2026-10-15-4a4dd: the anonymous findings channel names a project only to a config.read holder while
/// enforcement is on; the counts and Degraded describe what is shown.
/// </summary>
public sealed class StartupFindingsScopeTests
{
    private static readonly TokenAuthorityConfig Enforcing = new() { Enforce = true, NameClaim = "preferred_username" };

    private static readonly StartupFinding Installation = new("auth", StartupFindingSeverity.Advisory, "no authority configured");

    private static readonly StartupFinding OfBeta =
        new("pipelines", StartupFindingSeverity.Blocking, "Project beta is disabled.", Project: "beta");

    [Fact]
    public void StartupFindings_EnforcingCallerWithoutConfigRead_SeesNoProjectNamingFindingAndCountsMatch()
    {
        var mapping = Mapping(Binding("alpha", groups: ["team"], roles: [BuiltInRoles.Operator]));

        var answer = Read(Enforcing, mapping, ResolverUnderTest.Caller(Enforcing, ("groups", "team")));

        answer.Findings.Select(f => f.Reason).Should().Equal(Installation.Reason);
        (answer.Degraded, answer.Blocking, answer.Advisory).Should().Be((false, 0, 1));
        Read(Enforcing, mapping, new ClaimsPrincipal()).Findings.Should().HaveCount(1, "an anonymous caller is held the same");
    }

    [Fact]
    public void StartupFindings_ConfigReader_SeesEveryFinding()
    {
        var mapping = Mapping();
        mapping.GroupRoles["admins"] = [BuiltInRoles.Admin];

        var answer = Read(Enforcing, mapping, ResolverUnderTest.Caller(Enforcing, ("groups", "admins")));

        answer.Findings.Should().HaveCount(2);
        answer.Degraded.Should().BeTrue();
    }

    [Fact]
    public void StartupFindings_EnforceOff_AnswersAsToday()
    {
        var off = new TokenAuthorityConfig { NameClaim = "preferred_username" };

        var answer = Read(off, Mapping(), new ClaimsPrincipal());

        answer.Should().BeEquivalentTo(StartupFindingsResponse.From([Installation, OfBeta]));
    }

    private static StartupFindingsResponse Read(TokenAuthorityConfig auth, RoleMappingConfig mapping, ClaimsPrincipal user) =>
        StartupFindingsEndpoints.Read(new Recorded(), new NoMismatch(), null, user,
                ProjectScopeUnderTest.Access(auth, ResolverUnderTest.Over(auth, new StoredMappingStub(mapping)), "alpha", "beta"),
                pass: null, CancellationToken.None).GetAwaiter().GetResult()
            .Should().BeOfType<Ok<StartupFindingsResponse>>().Which.Value!;

    private sealed class Recorded : IStartupFindings
    {
        public IReadOnlyList<StartupFinding> All => [Installation, OfBeta];

        public void Record(StartupFinding finding) => throw new NotSupportedException();

        public void Clear(string subsystem) => throw new NotSupportedException();
    }

    private sealed class NoMismatch : IBuildMismatchDetector
    {
        public IReadOnlyList<StartupFinding> Compare(string? callerRevision) => [];
    }
}
