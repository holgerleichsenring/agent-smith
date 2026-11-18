using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.Specs;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863b: a deployment template is opened read-only for the coding master, which is told how the
/// context is delivered, the bound environments' values, and to ask for a missing parameter and name secrets only.</summary>
public sealed class DeploymentTemplateInLookTests
{
    private const string Address = "template:deploy:default";

    [Fact]
    public void Detail_DeploymentDeclaration_NamesTheDeclarationEnvironmentsAndRules()
    {
        var detail = TemplatePromptSection.Detail(Deployment(), "abc123");

        detail.Should().Contain("the `k8s-gitops` context of `delivery` at `abc123`")
            .And.Contain("this project's `default` context is DELIVERED")
            .And.Contain("`.agentsmith/contexts/k8s-gitops/deployment.yaml`")
            .And.Contain("`test` (manifest in this context's own repository; delivered without a person's approval; parameters `replicas`=`1`)")
            .And.Contain("`prod` (manifest in `gitops`; a person approves each delivery; no parameter values)")
            .And.Contain("asked with the value you recommend")
            .And.Contain("referenced by its name in the files and never written as a value");
    }

    [Fact]
    public void Detail_CodeDeclaration_IsUnchanged() =>
        TemplatePromptSection.Detail(CodingMasterTemplateTests.ProjectWithTemplate().Templates[0], "abc123")
            .Should().Be("the `server` context of `reference-project` at `abc123`, built after for this project's "
                + "`default` context; its files are under `.agentsmith/contexts/server/`");

    [Fact]
    public void Of_DeploymentBinding_IsSelectedUnderItsOwnAddress() =>
        TemplateDeclarations.Of(Project(), [], contexts: null).Keys.Should().Equal(Address);

    [Fact]
    public async Task Section_DeploymentTemplate_IsDescribedAndGivesNoCodeAdvice()
    {
        var files = new InMemorySandboxFileReader();
        files.Files["/work/.agentsmith/principles.md"] = "# Root rules\nKeep it small.\n";
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["Sample.Server"] = new Mock<ISandbox>().Object });
        pipeline.Set(ContextKeys.ProjectConfig, Project());

        await using var attachment = await CodingMasterTemplateTests
            .Scopes(new CodingMasterTemplateTests.RecordingScope(), new DerivationTestLooks.FixedReaderFactory(files))
            .OpenAsync(pipeline, new SpecDraft("2026-11-18-3863b", "goal", "yaml", []) { Contexts = ["default"] },
                readOnlySurface: false, CancellationToken.None);
        var section = attachment.Section(attachment.Names);

        section.Should().Contain($"`{Address}` — the `k8s-gitops` context").And.Contain("DELIVERED");
        section.Should().NotContain("advises", "a deployment template's principles advise code, not delivery files")
            .And.NotContain("Keep it small.");
    }

    private static ResolvedProject Project() => new() { Name = "sample", Templates = [Deployment()] };

    private static ProjectTemplate Deployment() => new("default", "k8s-gitops", "v1",
        new RepoConnection { Name = "delivery", Url = "https://stub.test/delivery" }, null, TemplateKinds.Deployment,
        [
            new DeploymentEnvironment("test", null, false, new Dictionary<string, string> { ["replicas"] = "1" }),
            new DeploymentEnvironment("prod", new RepoConnection { Name = "gitops", Url = "https://stub.test/gitops" },
                true, new Dictionary<string, string>()),
        ]);
}
