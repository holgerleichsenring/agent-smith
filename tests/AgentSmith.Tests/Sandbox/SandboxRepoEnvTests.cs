using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992d: <c>sandbox.secrets.repo_env</c> scopes a variable to one repository. It is read with the same
/// reference rule as <c>env</c>, checked by the same declaration review, and resolved into bindings that name their
/// repository; <c>env</c> stays project-wide.
/// </summary>
public sealed class SandboxRepoEnvTests
{
    private static SandboxSecrets Declared(string reference = "api-creds:db", string name = "DB_PASSWORD", string repo = "api") => new()
    {
        Env = new Dictionary<string, string> { ["FEED_TOKEN"] = "build:feed" },
        RepoEnv = new Dictionary<string, Dictionary<string, string>>
        {
            [repo] = new() { [name] = reference },
        },
    };

    [Fact]
    public void Resolve_RepoEnv_BindsEachVariableToItsRepository_EnvStaysProjectWide()
    {
        var resolved = new SandboxSecretsResolver().Resolve(new SandboxConfig { Secrets = Declared() });

        resolved.Env.Should().ContainSingle().Which.Should().Be(
            new SecretEnvBinding("FEED_TOKEN", new SecretRef("build", "feed")));
        resolved.RepoEnv.Should().ContainSingle().Which.Should().Be(
            new SecretEnvBinding("DB_PASSWORD", new SecretRef("api-creds", "db"), "api"));
        resolved.Count.Should().Be(2);
    }

    [Fact]
    public void Resolve_AMalformedRepoEnvReference_IsRefusedNamingTheRepositoryAndVariable()
    {
        var act = () => new SandboxSecretsResolver().Resolve(new SandboxConfig { Secrets = Declared("no-colon") });

        act.Should().Throw<ArgumentException>().WithMessage("*repo_env['api']['DB_PASSWORD']*secretName:key*")
            .Which.Message.Should().NotContain("no-colon", "a mistyped field may hold the value it meant to reference");
    }

    [Fact]
    public void Problems_RepoEnv_IsReviewedLikeEnv()
    {
        SandboxSecretDeclarationReview.Problems(Declared()).Should().BeEmpty();
        SandboxSecretDeclarationReview.Problems(Declared("no-colon")).Should().ContainSingle()
            .Which.Should().Contain("repo_env['api']['DB_PASSWORD']").And.Contain("'secretName:key'");
        SandboxSecretDeclarationReview.Problems(Declared(name: "1-bad")).Should().ContainSingle()
            .Which.Should().Contain("not a usable environment variable name");
        SandboxSecretDeclarationReview.Problems(Declared(repo: " ")).Should().ContainSingle()
            .Which.Should().Contain("names no repository");
    }

    [Fact]
    public void Problems_ARepoEnvKeyNamingNoRepositoryOfTheProject_IsRefusedListingTheExactNames()
    {
        SandboxSecretDeclarationReview.Problems(Declared(), ["api", "web"]).Should().BeEmpty();
        SandboxSecretDeclarationReview.Problems(Declared(repo: "API"), ["web", "api"]).Should().ContainSingle()
            .Which.Should().Contain("repo_env['API'] names no repository").And.Contain("exact names: api, web",
                "names match exactly, as the sandbox's scope does");
    }

    [Fact]
    public void DeclaredCount_CountsEveryRepositorysVariables()
    {
        Declared().DeclaredCount().Should().Be(2);
        new SandboxSecrets().DeclaredCount().Should().Be(0);
    }

    [Fact]
    public void Script_TestsTheGivenRepositorysScopedVariablesOnly()
    {
        var secrets = new ResolvedSandboxSecrets([], [],
            [new SecretEnvBinding("API_DB", new SecretRef("a", "b"), "api"), new SecretEnvBinding("WEB_NPM", new SecretRef("a", "b"), "web")]);

        SandboxSecretPresenceProbe.Script(secrets, "api").Should().Contain("API_DB").And.NotContain("WEB_NPM");
        SandboxSecretPresenceProbe.Script(secrets).Should().BeEmpty("without a repository no scoped variable is visible");
    }
}
