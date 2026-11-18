using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863a: a deployment binding owns its own address beside a code binding of the same context, and
/// a code binding's address is exactly what it was.</summary>
public sealed class DeploymentBindingAddressTests
{
    private static readonly RepoConnection Repo = new() { Name = "delivery", Url = "https://git.test/delivery" };

    [Theory]
    [InlineData(null, "template:api")]
    [InlineData("api-repo", "template:api-repo/api")]
    public void For_CodeBinding_KeepsItsAddress(string? contextRepo, string expected) =>
        TemplateScopeName.For(Binding(TemplateKinds.Code, contextRepo)).Should().Be(expected);

    [Theory]
    [InlineData(null, "template:deploy:api")]
    [InlineData("api-repo", "template:deploy:api-repo/api")]
    public void For_DeploymentBinding_IsMarked(string? contextRepo, string expected) =>
        TemplateScopeName.For(Binding(TemplateKinds.Deployment, contextRepo)).Should().Be(expected);

    [Fact]
    public void Owns_CodeAndDeploymentBindingOfOneContext_BothOwnTheirs()
    {
        var declarations = new[] { Binding(TemplateKinds.Code), Binding(TemplateKinds.Deployment) };

        TemplateScopeName.Owns(declarations, 0).Should().BeTrue();
        TemplateScopeName.Owns(declarations, 1).Should().BeTrue("the deployment binding's address is its own");
    }

    [Fact]
    public void Of_DeploymentBinding_IsNotSelected()
    {
        var project = new ResolvedProject { Templates = [Binding(TemplateKinds.Code), Binding(TemplateKinds.Deployment)] };

        TemplateDeclarations.Of(project, [], contexts: null).Keys.Should().Equal("template:api");
    }

    private static ProjectTemplate Binding(string kind, string? contextRepo = null) =>
        new("api", "k8s-gitops", "v1", Repo, contextRepo, kind, []);
}
