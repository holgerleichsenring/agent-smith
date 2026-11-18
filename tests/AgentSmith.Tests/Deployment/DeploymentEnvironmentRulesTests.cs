using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863a: what a binding's kind and environments may not be.</summary>
public sealed class DeploymentEnvironmentRulesTests
{
    [Fact]
    public void Check_SoundDeploymentBinding_Passes() =>
        DeploymentEnvironmentRules.Check("app", Binding(TemplateKinds.Deployment, Env("test"), Env("prod", "gitops")))
            .Should().BeEmpty();

    [Fact]
    public void Check_CodeBindingWithoutEnvironments_Passes() =>
        DeploymentEnvironmentRules.Check("app", Binding(null)).Should().BeEmpty();

    [Fact]
    public void Check_UnknownKind_IsRefused() =>
        DeploymentEnvironmentRules.Check("app", Binding("release")).Should().ContainSingle()
            .Which.Should().Contain("kind 'release'");

    [Fact]
    public void Check_EnvironmentsOnACodeBinding_AreRefused() =>
        DeploymentEnvironmentRules.Check("app", Binding(TemplateKinds.Code, Env("test"))).Should().ContainSingle()
            .Which.Should().Contain("is not of kind 'deployment'");

    [Fact]
    public void Check_UnnamedAndRepeatedEnvironments_AreRefused() =>
        DeploymentEnvironmentRules.Check("app", Binding(TemplateKinds.Deployment, Env(""), Env("test"), Env("TEST")))
            .Should().HaveCount(2).And.Contain(m => m.Contains("without a name")).And.Contain(m => m.Contains("twice"));

    [Fact]
    public void Check_GlobEnvironmentRepo_IsRefused() =>
        DeploymentEnvironmentRules.Check("app", Binding(TemplateKinds.Deployment, Env("test", "conn/*")))
            .Should().ContainSingle().Which.Should().Contain("glob repo ref");

    [Fact]
    public void DraftCheck_UnnamedEnvironment_ReportsNothingWhileItIsTyped()
    {
        var project = new ProjectEntity("app", "a", "t", ["target"], "code", ["code"],
            Templates: [Binding(TemplateKinds.Deployment, Env("")) with { Project = "nope" }]);
        var catalog = new ConfigCatalog([], [], [], [project], [], [], [], []);

        ProjectTemplateDraftCheck.MessagesFor(project, catalog).Should().BeEmpty();
    }

    private static TemplateReference Binding(string? kind, params DeploymentEnvironmentReference[] environments) =>
        new("api", "platform", "delivery", "k8s-gitops", "v1", null, kind, environments);

    private static DeploymentEnvironmentReference Env(string name, string? repo = null) => new(name, repo);
}
