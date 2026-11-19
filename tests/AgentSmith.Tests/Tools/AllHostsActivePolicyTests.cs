using AgentSmith.Application.Services.Tools;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

public sealed class AllHostsActivePolicyTests
{
    [Fact]
    public void GetAllowedHosts_FixBug_ReturnsAllHosts()
    {
        var policy = new AllHostsActivePolicy();

        var allowed = policy.GetAllowedHosts("code");

        // 2026-11-18-9c1cd: no GetArtifactCredentialsToolHost — registry auth is staged before the master.
        // p0193: + WriteContextYamlToolHost (typed context.yaml write path).
        allowed.Should().BeEquivalentTo(new[]
        {
            typeof(FilesystemToolHost),
            typeof(LogDecisionToolHost),
            typeof(HumanToolHost),
            typeof(WebToolHost),
            typeof(SpawnAgentToolHost),
            typeof(WriteContextYamlToolHost),
        });
    }

    [Fact]
    public void GetAllowedHosts_UnknownPipeline_ReturnsAllHosts()
    {
        var policy = new AllHostsActivePolicy();

        var allowed = policy.GetAllowedHosts("invoice-processor-not-yet-registered");

        allowed.Should().HaveCount(6);
    }

    [Fact]
    public void GetAllowedHosts_WildcardSentinel_ReturnsAllHosts()
    {
        var policy = new AllHostsActivePolicy();

        var allowed = policy.GetAllowedHosts(IToolKit.WildcardPipelineName);

        allowed.Should().HaveCount(6);
    }

    [Fact]
    public void GetAllowedHosts_NullPipelineName_Throws()
    {
        var policy = new AllHostsActivePolicy();

        var act = () => policy.GetAllowedHosts(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
