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
/// Bug regression: TryReadFileAsync + ListDirectoryAsync went through
/// GetDefaultBranchAsync without respecting a configured DefaultBranch on
/// AzureReposSourceConnection. After the fix (+ p0179 follow-up), the
/// configured branch flows all the way down into the Azure SDK call's
/// GitVersionDescriptor. These tests pin that path so an operator who sets
/// default_branch: develop on a catalog entry sees the discovery /
/// context-yaml read hit the develop ref, not main.
///
/// Without this coverage the bug recurs silently: GetDefaultBranchAsync
/// could be refactored to skip the configured override and the build would
/// still pass.
/// </summary>
public sealed class AzureReposSourceProviderConfiguredBranchE2eTests
{
    private const string OrgUrl = "https://dev.azure.com/example";
    private const string Project = "demo";
    private const string Repo = "repo";
    private const string Pat = "azdo-pat";
    private const string ConfiguredBranch = "develop";

    [Fact]
    public async Task TryReadFileAsync_ConfiguredDefaultBranch_PassedToAzureSdkVersionDescriptor()
    {
        var (sut, gitClientMock, captured) = BuildSut();
        gitClientMock.Setup(c => c.GetItemContentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<VersionControlRecursionType?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<GitVersionDescriptor>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, VersionControlRecursionType?, bool?, bool?, bool?,
                      GitVersionDescriptor, bool?, bool?, bool?, object, CancellationToken>(
                (_, _, _, _, _, _, _, _, vd, _, _, _, _, _) => captured.VersionDescriptor = vd)
            .ReturnsAsync(() => new MemoryStream(System.Text.Encoding.UTF8.GetBytes("yaml: ok")));

        await sut.TryReadFileAsync(".agentsmith/contexts/api/context.yaml", CancellationToken.None);

        captured.VersionDescriptor.Should().NotBeNull();
        captured.VersionDescriptor!.Version.Should().Be(ConfiguredBranch);
        captured.VersionDescriptor.VersionType.Should().Be(GitVersionType.Branch);
    }

    [Fact]
    public async Task ListDirectoryAsync_ConfiguredDefaultBranch_PassedToAzureSdkVersionDescriptor()
    {
        // Strategy: don't pin the exact GetItemsAsync overload signature
        // (the SDK exposes multiple, distinguished by repositoryId type +
        // a long optional-parameter tail that has shifted between SDK
        // versions). Instead, after the call, inspect Mock.Invocations and
        // find the GitVersionDescriptor argument by type.
        var (sut, gitClientMock, _) = BuildSut();

        try { await sut.ListDirectoryAsync(".agentsmith/contexts", CancellationToken.None); }
        catch { /* GetItemsAsync default null result throws downstream; that's fine — we only need the invocation captured */ }

        var descriptor = gitClientMock.Invocations
            .Where(i => i.Method.Name == "GetItemsAsync")
            .SelectMany(i => i.Arguments)
            .OfType<GitVersionDescriptor>()
            .FirstOrDefault();

        descriptor.Should().NotBeNull("ListDirectoryAsync must invoke GetItemsAsync with a GitVersionDescriptor");
        descriptor!.Version.Should().Be(ConfiguredBranch);
        descriptor.VersionType.Should().Be(GitVersionType.Branch);
    }

    private static (AzureReposSourceProvider Sut, Mock<GitHttpClient> GitClientMock, Capture Captured) BuildSut()
    {
        var captured = new Capture();
        var gitClientMock = new Mock<GitHttpClient>(
            new Uri("https://localhost/fake"),
            new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        var factoryMock = new Mock<IAzDoClientFactory>();
        factoryMock.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(gitClientMock.Object);

        var sut = new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, DefaultBranch: ConfiguredBranch),
            factoryMock.Object,
            NullLogger<AzureReposSourceProvider>.Instance);
        return (sut, gitClientMock, captured);
    }

    private sealed class Capture
    {
        public GitVersionDescriptor? VersionDescriptor { get; set; }
    }
}
