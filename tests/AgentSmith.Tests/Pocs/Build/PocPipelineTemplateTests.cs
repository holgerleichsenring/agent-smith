using AgentSmith.Application.Services.Pocs.Pipeline;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Build;

/// <summary>2026-11-23-933dd: the build pipeline per host — dispatch only, the host's input keys via env, the guard,
/// the checkout at the sha of the folder alone, a timeout per job, the gzip artifact, the Trivy report and gate, and the
/// push credential in the push job alone.</summary>
public sealed class PocPipelineTemplateTests
{
    internal static PocPipelineTemplate Template()
    {
        var resources = new PocPipelineResources();
        var fill = new PocYamlFill();
        var scripts = new PocPipelineScripts(resources, fill);
        return new PocPipelineTemplate(new GitHubPocWorkflow(resources, scripts, fill), new AzurePocPipeline(resources, scripts, fill),
            new GitLabPocCi(resources, scripts, fill));
    }

    internal static PocPipelineValues Values(string root = "apps.v2/pocs") =>
        PocPipelineValues.Of(new PocTargetConfig { Name = "main", Root = root, Registry = PocsConfigTests.SoundRegistry() },
            new PocScanConfig { TrivyImage = PocsConfigTests.PinnedTrivyImage });

    private static string Render(RepoType host) => Template().Render(host, Values()).File!.Content;

    [Fact]
    public void Render_GitHub_IsDispatchOnly_ChecksOutTheFolderAtTheSha_AndOnlyPushHoldsTheCredential()
    {
        var file = Template().Render(RepoType.GitHub, Values()).File!;
        var yaml = file.Content;

        file.Path.Should().Be(".github/workflows/poc-build.yml");
        yaml.Should().StartWith(PocPipelineTemplate.Marker).And.Contain("  workflow_dispatch:").And.NotContain("  push:\n    branches")
            .And.Contain("permissions:\n  contents: read").And.Contain("POC_SHA: ${{ inputs.sha }}")
            .And.Contain("ref: ${{ inputs.sha }}").And.Contain("sparse-checkout: ${{ format('/{0}/', inputs.folder) }}")
            .And.Contain("persist-credentials: false").And.Contain("timeout-minutes: 12").And.Contain("timeout-minutes: 5")
            .And.Contain("timeout-minutes: 3").And.Contain("retention-days: 1").And.Contain("name: poc-scan-report")
            .And.Contain("if: always()").And.Contain("--ignore-policy poc-gate.rego").And.Contain("--exit-code 1")
            .And.Contain("--template \"@poc-report.tpl\"").And.Contain(PocsConfigTests.PinnedTrivyImage);
        yaml.Split("secrets.").Should().HaveCount(3, "the push step alone names the two push secrets");
        yaml.IndexOf("secrets.POC_REGISTRY_USER", StringComparison.Ordinal).Should().BeGreaterThan(yaml.IndexOf("  push:", StringComparison.Ordinal));
        yaml.IndexOf("environment: poc-push", StringComparison.Ordinal).Should().BeGreaterThan(yaml.IndexOf("  push:", StringComparison.Ordinal));
        yaml.Should().NotContain("@@");
    }

    [Fact]
    public void Render_EveryHost_GuardsEachInput_WithTheRootRegexEscaped()
    {
        foreach (var host in new[] { RepoType.GitHub, RepoType.AzureDevOps, RepoType.GitLab })
        {
            var yaml = Render(host);
            yaml.Should().Contain("poc_check folder \"$POC_FOLDER\" 'apps\\.v2/pocs/[a-z0-9-]+/[a-z0-9-]+'", host.ToString())
                .And.Contain("poc_check sha \"$POC_SHA\" '[0-9a-f]{40}'")
                .And.Contain("[A-Z][A-Z0-9]*-[A-Za-z0-9._:-]{1,80}").And.Contain("'[a-z0-9-]{1,80}(,[a-z0-9-]{1,80})*'")
                .And.Contain("poc_check ignore_unfixed \"$POC_IGNORE_UNFIXED\" 'true|false'")
                .And.Contain("blocking_secret_rules := {$rules}").And.Contain("max(v3_scores) < 9.0")
                .And.Contain("docker save poc-image > out/image.tar").And.Contain("gzip out/image.tar")
                .And.Contain("pocregistry.azurecr.io/pocs/$POC_NAME:$POC_SHA");
        }
    }

    [Fact]
    public void Render_Azure_FetchesTheShaWithTheJobTokenInThatStepOnly_AndUsesTheServiceConnectionInPush()
    {
        var file = Template().Render(RepoType.AzureDevOps, Values() with { TemplatePath = "poc-template" }).File!;
        var yaml = file.Content;

        file.Path.Should().Be("poc-template/azure-pipelines.poc-build.yml");
        yaml.Should().Contain("trigger: none\npr: none").And.Contain("- checkout: none")
            .And.Contain("POC_SHA: ${{ parameters.sha }}").And.Contain("git fetch -q --depth 1 --filter=blob:none origin \"$POC_SHA\"")
            .And.Contain("git checkout -q FETCH_HEAD").And.Contain("timeoutInMinutes: 12");
        yaml.Split("$(System.AccessToken)").Should().HaveCount(2, "only the fetch step maps the job token");
        yaml.Split("containerRegistry: poc-registry").Should().HaveCount(2);
        yaml.IndexOf("containerRegistry", StringComparison.Ordinal).Should().BeGreaterThan(yaml.IndexOf("- job: push", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_GitLab_RulesEveryJobToTheApi_UsesDind_AndScopesTheCredentialToPush()
    {
        var file = Template().Render(RepoType.GitLab, Values()).File!;
        var yaml = file.Content;

        file.Path.Should().Be(".gitlab-ci.yml");
        yaml.Should().Contain("GIT_STRATEGY: none").And.Contain("$CI_PIPELINE_SOURCE == \"api\" && $POC_FOLDER")
            .And.Contain("docker:27-dind").And.Contain("POC_REPO_URL=\"$CI_REPOSITORY_URL\"").And.Contain("timeout: 12m")
            .And.Contain("expire_in: 1 day").And.Contain("when: always");
        yaml.Split("<<: *poc-rules").Should().HaveCount(4, "every job is ruled");
        yaml.Split("environment:").Should().HaveCount(2, "the push job alone declares the environment");
        yaml.IndexOf("$POC_REGISTRY_PASSWORD", StringComparison.Ordinal).Should().BeGreaterThan(yaml.IndexOf("\npush:", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_ALocalHostOrAnUnsafeRoot_IsAProblem()
    {
        Template().Render(RepoType.Local, Values()).Problem.Should().Contain("runs no pipeline");
        Template().Render(RepoType.GitHub, Values("po'cs")).Problem.Should().Contain("root 'po'cs'");
    }

    [Theory]
    [InlineData(RepoType.GitHub)]
    [InlineData(RepoType.AzureDevOps)]
    [InlineData(RepoType.GitLab)]
    public void Render_EveryHost_IsYaml(RepoType host)
    {
        var yaml = Render(host);
        var dump = Environment.GetEnvironmentVariable("AGENTSMITH_DUMP_POC_PIPELINE");
        if (!string.IsNullOrEmpty(dump)) File.WriteAllText(Path.Combine(dump, host + ".yml"), yaml);

        var read = () => new YamlDotNet.Serialization.DeserializerBuilder().Build().Deserialize<object>(yaml);

        read.Should().NotThrow();
    }
}
