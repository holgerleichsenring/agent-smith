using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-25-8c97: an identity a build does not stamp is an identity no runtime can read.
/// The revision label was already computed in the publish workflow and thrown away, because
/// the build step passed no build arguments at all — so neither image could name its build
/// even though the value existed one step above it. These cases hold the plumbing: the
/// workflow passes the revision, and each image turns it into something readable at runtime.
/// </summary>
public sealed class BuildIdentityStampingTests
{
    private const string Workflow = ".github/workflows/docker-publish.yml";
    private const string ServerImage = "src/backend/AgentSmith.Server/Dockerfile";
    private const string DashboardImage = "src/dashboard/Dockerfile";

    [Fact]
    public void BuildIdentity_IsReadableAtRuntimeInBothImages()
    {
        Read(ServerImage).Should()
            .Contain($"ARG {BuildIdentity.RevisionVariable}")
            .And.Contain($"ENV {BuildIdentity.RevisionVariable}=${BuildIdentity.RevisionVariable}",
                "the server reads its build off the environment, so the build argument has "
                + "to become one");

        Read(DashboardImage).Should()
            .Contain($"ARG {BuildIdentity.RevisionVariable}")
            .And.Contain($"ENV NEXT_PUBLIC_BUILD_REVISION=${BuildIdentity.RevisionVariable}",
                "Next.js inlines a NEXT_PUBLIC_* value at build time, which is what lets a "
                + "downloaded bundle still name its build after its pod is gone");
    }

    [Fact]
    public void DashboardImage_StampsTheBuild_BeforeItBuildsTheBundle()
    {
        var text = Read(DashboardImage);

        text.IndexOf("ENV NEXT_PUBLIC_BUILD_REVISION", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("pnpm build", StringComparison.Ordinal),
                "a variable set after the bundle is compiled is never inlined into it");
    }

    [Fact]
    public void PublishWorkflow_PassesTheRevision_NotOnlyTheReleaseVersion()
    {
        var text = Read(Workflow);

        text.Should().Contain("build-args:", "the value existed and reached no container");
        text.Should().Contain(BuildIdentity.RevisionVariable + "=${{ github.sha }}",
            "version.txt moves only on a release commit while this workflow publishes on "
            + "every push to the trunk, so the commit is the only value that differs "
            + "between two builds of one release");
        text.Should().Contain($"{BuildIdentity.VersionVariable}=", "the release version "
            + "rides along because it is what an operator recognises");
    }

    private static string Read(string relative)
    {
        var path = Path.Combine(RepoRoot(), relative);
        File.Exists(path).Should().BeTrue($"{relative} must exist for this rule to mean anything");
        return File.ReadAllText(path);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "dashboard")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the test must find the repository root");
        return dir!.FullName;
    }
}
