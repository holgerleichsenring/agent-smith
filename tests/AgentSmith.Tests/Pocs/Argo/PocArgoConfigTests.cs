using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Argo;

/// <summary>2026-11-23-c81ec: the argo and rollout blocks' values, the PoC run budget, and the shared-Argo finding.</summary>
public sealed class PocArgoConfigTests
{
    [Fact]
    public void Sound_HasNoProblems() => PocsConfigTests.Sound().Problems().Should().BeEmpty();

    [Fact]
    public void Problems_AnEnabledTargetWithoutArgo_NamesTheKey()
    {
        var pocs = PocsConfigTests.Sound();
        pocs.Targets[0].Argo = null;

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("pocs.targets[].argo of target 'main' is missing");
        pocs.Enabled = false;
        pocs.Problems().Should().BeEmpty("the form may be filled in before the switch");
    }

    [Theory]
    [InlineData("url", "http://argocd.example.internal", "it must be an https URL")]
    [InlineData("token_warn_days", "-1", "two weeks leaves time")]
    [InlineData("generator_requeue_seconds", "181", "a third of the rollout deadline")]
    [InlineData("generator_requeue_seconds", "5", "must be 10 to 180")]
    [InlineData("project", "", "name the AppProject")]
    [InlineData("repo_url", "", "repository credential")]
    public void Problems_ABadArgoValue_NamesKeyValueAndReason(string key, string value, string reason)
    {
        var argo = PocsConfigTests.SoundArgo();
        _ = key switch
        {
            "url" => argo.Url = value,
            "token_warn_days" => (argo.TokenWarnDays = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "generator_requeue_seconds" => (argo.GeneratorRequeueSeconds = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "project" => argo.Project = value,
            _ => argo.RepoUrl = value,
        };

        argo.Problems("main").Should().ContainSingle().Which.Should().Contain($"pocs.targets[].argo.{key} of target 'main'").And.Contain(reason);
    }

    [Fact]
    public void Problems_RolloutAndBudget_NameValueAndKey()
    {
        var pocs = PocsConfigTests.Sound();
        (pocs.Rollout.PollSeconds, pocs.Rollout.DeadlineSeconds, pocs.Rollout.EndMarginSeconds, pocs.Rollout.CommitAllowanceSeconds) = (0, 30, 10, -1);
        pocs.RunWallTimeSeconds = 10;

        pocs.Problems().Should().HaveCount(5).And.Contain(p => p.Contains("pocs.rollout.poll_seconds is 0"))
            .And.Contain(p => p.Contains("pocs.rollout.deadline_seconds is 30")).And.Contain(p => p.Contains("30 s kill grace"))
            .And.Contain(p => p.Contains("pocs.rollout.commit_allowance_seconds is -1")).And.Contain(p => p.Contains("pocs.run_wall_time_seconds is 10"));
    }

    [Fact]
    public void Problems_TwoTargetsOnOneArgoWithOneNamespace_AreBlockingNamingBothAndTheKeys()
    {
        var pocs = PocsConfigTests.Sound();
        var second = PocsConfigTests.Sound().Targets[0];
        (second.Name, second.SourceRepo) = ("second", "gh/poc-src-2");
        second.Argo!.DestinationServer = "https://other-cluster.example.internal";
        pocs.Targets.Add(second);

        var finding = PocsConfigValidator.Findings(new AgentSmithConfig { Pocs = pocs }).Should().ContainSingle().Subject;

        finding.IsBlocking.Should().BeTrue();
        finding.Reason.Should().Contain("targets 'main' and 'second'").And.Contain("pocs.targets[].argo.url")
            .And.Contain("same pocs.targets[].argo.namespace").And.Contain("unique only per target");
    }

    [Fact]
    public void Problems_TwoTargetsOnOneArgoWithOwnNamespacesAndClusters_AreFine()
    {
        var pocs = PocsConfigTests.Sound();
        var second = PocsConfigTests.Sound().Targets[0];
        (second.Name, second.SourceRepo, second.Argo!.Namespace, second.Argo.DestinationServer) =
            ("second", "gh/poc-src-2", "argocd-2", "https://other-cluster.example.internal");
        pocs.Targets.Add(second);

        pocs.Problems().Should().BeEmpty();
    }
}
