using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// Regression: AzureReposSourceProvider.ListDirectoryAsync filtered returned
/// GitItem.Path entries with prefix "<scopePath>/" — but Azure DevOps returns
/// item paths with a leading "/" (e.g. "/.agentsmith/contexts/api"), so the
/// prefix check failed for EVERY item and the discovery returned []. The
/// SandboxLanguageResolver then fell back to SyntheticDefault ("default", ".")
/// — invisible for repos that happened to have a "default/" subfolder on
/// disk, fatal for multi-context monorepos where the per-context subfolders
/// (api/, clientapigenerator/, ...) are the actual structure.
/// </summary>
public sealed class AzureReposSourceProviderListDirectoryTests
{
    private const string OrgUrl = "https://dev.azure.com/example";
    private const string Project = "demo";
    private const string Repo = "repo";
    private const string Pat = "azdo-pat";

    [Fact]
    public async Task ListDirectoryAsync_AzureReturnsLeadingSlashPaths_ReturnsSubfolderNames()
    {
        // Azure DevOps GetItemsAsync returns items with leading "/". The
        // provider must strip / tolerate that and still return the immediate
        // child folder names.
        var items = new List<GitItem>
        {
            new() { Path = "/.agentsmith/contexts",                          IsFolder = true  },
            new() { Path = "/.agentsmith/contexts/api",                      IsFolder = true  },
            new() { Path = "/.agentsmith/contexts/clientapigenerator",       IsFolder = true  },
            new() { Path = "/.agentsmith/contexts/copyrheview",              IsFolder = true  },
            new() { Path = "/.agentsmith/contexts/databasemigrator",         IsFolder = true  },
            new() { Path = "/.agentsmith/contexts/treevalidator",            IsFolder = true  },
        };
        var sut = BuildSut(items);

        var result = await sut.ListDirectoryAsync(".agentsmith/contexts", CancellationToken.None);

        result.Should().BeEquivalentTo(new[]
        {
            "api", "clientapigenerator", "copyrheview", "databasemigrator", "treevalidator"
        });
    }

    [Fact]
    public async Task ListDirectoryAsync_FiltersOutTheScopePathItselfAndDeeperFiles()
    {
        var items = new List<GitItem>
        {
            new() { Path = "/.agentsmith/contexts",                          IsFolder = true  },
            new() { Path = "/.agentsmith/contexts/api",                      IsFolder = true  },
            new() { Path = "/.agentsmith/contexts/api/context.yaml",         IsFolder = false },
            new() { Path = "/.agentsmith/contexts/api/coding-principles.md", IsFolder = false },
            new() { Path = "/.agentsmith/contexts/docs",                     IsFolder = true  },
        };
        var sut = BuildSut(items);

        var result = await sut.ListDirectoryAsync(".agentsmith/contexts", CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { "api", "docs" });
    }

    [Fact]
    public async Task ListDirectoryAsync_EmptyApiResult_ReturnsEmpty()
    {
        var sut = BuildSut(new List<GitItem>());

        var result = await sut.ListDirectoryAsync(".agentsmith/contexts", CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static AzureReposSourceProvider BuildSut(List<GitItem> apiReturn)
    {
        // Pin GetItemsAsync via reflection: the SDK exposes several overloads
        // distinguished by repositoryId type (string vs Guid) + a long
        // optional-parameter tail. Reflecting once at test time matches
        // exactly the overload the provider calls, regardless of SDK churn.
        var gitClientMock = new Mock<GitHttpClient>(
            new Uri("https://localhost/fake"),
            new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        var getItemsMethod = typeof(GitHttpClient).GetMethods()
            .Where(m => m.Name == "GetItemsAsync")
            .Where(m => m.GetParameters().Length > 0
                     && m.GetParameters()[0].ParameterType == typeof(string)
                     && m.GetParameters().Length >= 2
                     && m.GetParameters()[1].ParameterType == typeof(string))
            .OrderBy(m => m.GetParameters().Length)
            .Last();

        var pars = getItemsMethod.GetParameters();
        var anyArgs = new System.Linq.Expressions.Expression[pars.Length];
        var itType = typeof(It);
        for (var i = 0; i < pars.Length; i++)
        {
            var isAnyOpen = itType.GetMethods().First(m => m.Name == "IsAny" && m.IsGenericMethod);
            var isAnyClosed = isAnyOpen.MakeGenericMethod(pars[i].ParameterType);
            anyArgs[i] = System.Linq.Expressions.Expression.Call(isAnyClosed);
        }
        var instance = System.Linq.Expressions.Expression.Parameter(typeof(GitHttpClient), "c");
        var call = System.Linq.Expressions.Expression.Call(instance, getItemsMethod, anyArgs);
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<GitHttpClient, Task<List<GitItem>>>>(call, instance);
        gitClientMock.Setup(lambda).ReturnsAsync(apiReturn);

        var factoryMock = new Mock<IAzDoClientFactory>();
        factoryMock.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(gitClientMock.Object);

        return new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, DefaultBranch: "develop"),
            factoryMock.Object,
            NullLogger<AzureReposSourceProvider>.Instance);
    }
}
