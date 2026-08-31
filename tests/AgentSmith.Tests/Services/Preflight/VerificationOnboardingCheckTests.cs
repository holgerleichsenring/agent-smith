using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// 2026-08-31-f634: onboarding names the repositories the tool can verify. The three
/// states are load-bearing and one of them is a trap: the sandbox resolver answers an
/// unreadable repository and an un-onboarded one identically, so a check built on
/// ResolveAllAsync would tell an operator whose credential failed to go and write
/// verification stages.
/// </summary>
public sealed class VerificationOnboardingCheckTests
{
    private static readonly ContextYamlVerifyStage Stage = new("build", "dotnet build");

    [Fact]
    public async Task Onboarding_ARepositoryDeclaringStages_IsReportedEligible()
    {
        var check = Check(new RemoteContextListing(
            [new RemoteContextDiscovery("backend", ".", "csharp", Verify: [Stage])]));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("estate-repo/backend: eligible").And.Contain("build");
        result.Message.Should().Contain("no sandbox");
    }

    [Fact]
    public async Task Onboarding_ARepositoryDeclaringNothing_IsReportedNotYetDeclared()
    {
        var check = Check(new RemoteContextListing(
            [new RemoteContextDiscovery("backend", ".", "csharp")]));

        var result = await check.RunAsync(CancellationToken.None);

        // The normal state of an un-onboarded repository: reported, never a finding.
        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("estate-repo/backend: not yet declared");
    }

    [Fact]
    public async Task Onboarding_ARepositoryThatCannotBeListed_IsReportedUnreadable()
    {
        var check = Check(RemoteContextListing.Unreadable("401 Unauthorized"));

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("unreadable").And.Contain("401 Unauthorized");
        result.Message.Should().NotContain("not yet declared");
        result.FixHint.Should().Contain("auth secret");
    }

    [Fact]
    public async Task RunAsync_AListingWithNoContextAtAll_IsNotYetDeclaredForTheRepository()
    {
        var check = Check(RemoteContextListing.None);

        var result = await check.RunAsync(CancellationToken.None);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("estate-repo: not yet declared");
    }

    [Fact]
    public async Task RunAsync_ConfigFailedToLoad_Skips()
    {
        var check = new VerificationOnboardingCheck(
            FakePreflightConfigSource.LoadFailure("boom"), new Mock<ISandboxLanguageResolver>().Object);

        (await check.RunAsync(CancellationToken.None)).Status.Should().Be(PreflightStatus.Skip);
    }

    private static VerificationOnboardingCheck Check(RemoteContextListing listing)
    {
        var resolver = new Mock<ISandboxLanguageResolver>();
        resolver
            .Setup(r => r.ListContextsAsync(It.IsAny<RepoConnection>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listing);
        var config = new AgentSmithConfig
        {
            Repos = new Dictionary<string, RepoConnection>
            {
                ["estate-repo"] = new() { Name = "estate-repo", Url = "https://example.test/r" },
            },
        };
        return new VerificationOnboardingCheck(FakePreflightConfigSource.Of(config), resolver.Object);
    }
}
