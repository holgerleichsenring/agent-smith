using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;

namespace AgentSmith.Tests.Providers;

/// <summary>
/// 2026-11-25-d0e2: an Entra access token goes to Azure DevOps as Bearer — the header, the client library's OAuth credential and
/// git's bearer extraheader — and a personal access token as Basic, decided from the token itself.
/// </summary>
public sealed class AzureDevOpsAuthTests
{
    private const string Entra = "eyJ0eXAiOiJKV1QifQ.eyJhdWQiOiI0OTliIn0.c2lnbmF0dXJl";
    private const string Pat = "abcdefghijklmnopqrstuvwxyz234567abcdefghijklmnopqrst";

    [Fact]
    public void Bearer_IsAJwt_NeverAPat()
    {
        AzureDevOpsBearer.Is(Entra).Should().BeTrue();
        AzureDevOpsBearer.Is(Pat).Should().BeFalse();
        AzureDevOpsBearer.Is("eyJ.only-one-dot").Should().BeFalse();
        AzureDevOpsBearer.Is(null).Should().BeFalse();
    }

    [Fact]
    public void Header_SendsAnEntraTokenAsBearer_AndAPatAsBasicWithAnEmptyUser()
    {
        var bearer = AzureDevOpsAuth.Header(Entra);
        (bearer.Scheme, bearer.Parameter).Should().Be(("Bearer", Entra));

        var basic = AzureDevOpsAuth.Header(Pat);
        basic.Scheme.Should().Be("Basic");
        Encoding.ASCII.GetString(Convert.FromBase64String(basic.Parameter!)).Should().Be($":{Pat}");
    }

    [Fact]
    public void Credential_IsTheOAuthAccessTokenCredential_ForAnEntraToken()
    {
        AzureDevOpsAuth.Credential(Entra).Federated.Should().BeOfType<VssOAuthAccessTokenCredential>();
        AzureDevOpsAuth.Credential(Pat).Federated.Should().BeOfType<VssBasicCredential>();
    }

    [Fact]
    public void GitTokenResolver_MarksAnAzureReposEntraToken_AsBearer()
    {
        var resolver = new GitTokenResolver(new CredentialResolver(new LoadedSecretValues(
            TestCredentials.Config(("sp", Entra), ("pat", Pat)))));
        var azure = new RepoConnection { Name = "app", Type = RepoType.AzureDevOps, Url = "https://dev.azure.com/o/p/_git/app", Auth = "sp" };

        resolver.For(azure).Should().Be(new GitCredential(Entra, true, "https://dev.azure.com/"));
        resolver.For(azure with { Auth = "pat" }).Bearer.Should().BeFalse();
        resolver.For(azure with { Type = RepoType.GitHub }).Bearer.Should().BeFalse("only Azure Repos takes the bearer header");
    }

    [Fact]
    public void GitCredential_Bearer_CarriesTheHeaderThroughConfigEnv_AndLeavesGitConfigCountAlone()
    {
        var bearer = new GitCredential(Entra, Bearer: true);

        bearer.Args("helper").Should().Equal($"--config-env=http.extraheader={GitCredential.BearerHeaderVariable}", "-c", "helper");
        (bearer with { Scope = "https://dev.azure.com/" }).Args("helper")[0].Should()
            .Be($"--config-env=http.https://dev.azure.com/.extraheader={GitCredential.BearerHeaderVariable}");
        bearer.Env().Should().Contain(GitCredential.BearerHeaderVariable, $"AUTHORIZATION: bearer {Entra}")
            .And.ContainKey("GIT_TOKEN").And.NotContainKey("GIT_CONFIG_COUNT");

        var pat = new GitCredential(Pat);
        pat.Args("helper").Should().Equal("-c", "helper");
        pat.Env().Should().BeEquivalentTo(new Dictionary<string, string> { ["GIT_TOKEN"] = Pat });
        GitCredential.None.Env().Should().BeNull();
    }
}
