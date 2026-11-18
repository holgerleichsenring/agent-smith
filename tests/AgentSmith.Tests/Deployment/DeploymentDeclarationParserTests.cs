using AgentSmith.Application.Services.Deployment;
using FluentAssertions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863c: a template's deployment.yaml, read into what a deploy run acts on.</summary>
public sealed class DeploymentDeclarationParserTests
{
    private const string Declaration = """
        delivery: gitops_pr
        files: [Dockerfile, deploy/]
        parameters:
          - name: image
        secrets:
          - name: DATABASE_URL
        environments:
          - name: test
            manifest: deploy/overlays/test/kustomization.yaml
            version_key: images.0.newTag
          - name: prod
            manifest: deploy/overlays/prod/kustomization.yaml
            version_key: images.0.newTag
            promotes_from: test
        """;

    private readonly DeploymentDeclarationParser _parser = new();

    [Fact]
    public void Parse_FullDeclaration_ReadsTheEnvironments()
    {
        var (declaration, error) = _parser.Parse(Declaration);

        error.Should().BeNull();
        declaration!.Delivery.Should().Be(DeploymentDeclaration.GitOpsPullRequest);
        declaration.Environment("PROD")!.PromotesFrom.Should().Be("test");
        declaration.Environment("test")!.Should().Be(new DeclaredEnvironment(
            "test", "deploy/overlays/test/kustomization.yaml", "images.0.newTag", null));
    }

    [Fact]
    public void Parse_EnvironmentWithoutManifest_NamesIt() =>
        _parser.Parse(Declaration.Replace("    manifest: deploy/overlays/prod/kustomization.yaml\n", ""))
            .Error.Should().Be("environment 'prod' has no manifest");

    [Fact]
    public void Parse_UnknownDelivery_IsRefused() =>
        _parser.Parse(Declaration.Replace("gitops_pr", "pipeline")).Error.Should().Contain("delivery 'pipeline'");

    [Fact]
    public void Parse_PipelineTrigger_ReadsPipelineRefInputsAndParameterDefaults()
    {
        var (declaration, error) = _parser.Parse(DeployPipelineTriggerTests.Declaration);

        error.Should().BeNull();
        declaration!.Delivery.Should().Be(DeploymentDeclaration.PipelineTrigger);
        declaration.Parameters!["image"].Should().Be("registry.example/app");
        var prod = declaration.Environment("prod")!;
        prod.Manifest.Should().BeNull("a pipeline environment's manifest is optional");
        prod.Pipeline!.Ref.Should().Be("release");
        prod.Pipeline.Inputs["tag"].Should().Be("{context}-{version}");
    }

    [Theory]
    [InlineData("inputs: { tag: \"{context}-{version}\" }", "inputs: { tag: \"{context}\" }", "passes the version in none of its inputs")]
    [InlineData("inputs: { tag: \"{context}-{version}\" }", "inputs: { tag: \"{cluster}-{version}\" }", "name {cluster}")]
    [InlineData("    version_key: images.0.newTag\n", "", "has no version_key")]
    public void Parse_PipelineTriggerMistakes_AreRefused(string from, string to, string error) =>
        _parser.Parse(DeployPipelineTriggerTests.Declaration.Replace(from, to)).Error.Should().Contain(error);

    [Fact]
    public void Parse_NotYaml_IsRefused() =>
        _parser.Parse("environments: [").Error.Should().Contain("not readable YAML");
}
