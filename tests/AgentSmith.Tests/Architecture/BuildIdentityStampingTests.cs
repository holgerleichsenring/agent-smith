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
    private const string CliImage = "src/backend/AgentSmith.Cli/Dockerfile";
    private const string ComposeExample = "deploy/docker-compose.example.yml";

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

    /// <summary>
    /// 2026-09-25-9c4d: the rule used to read the two Dockerfiles and the publish workflow, and
    /// nothing else — so it stayed green while every LOCAL build was mis-stamped. A server built
    /// minutes ago reported a revision 496 commits old, because the operator's own compose file
    /// carried a frozen sha in its build args. That file is gitignored as a leak class and cannot
    /// be reached from here; what CAN be fixed is the example every operator copies, which
    /// declared no build args at all.
    /// </summary>
    [Fact]
    public void ComposeExample_EveryBuiltService_PassesTheIdentityArgs()
    {
        var text = Read(ComposeExample);
        var builds = text.Split('\n').Count(l => l.TrimStart().StartsWith("dockerfile:", StringComparison.Ordinal));

        builds.Should().BeGreaterThan(0, "the example builds something");
        CountOf(text, BuildIdentity.RevisionVariable).Should().Be(builds,
            "an image built without the revision cannot say which tree it came from");
        CountOf(text, BuildIdentity.VersionVariable).Should().Be(builds);
    }

    /// <summary>
    /// The null-value form, not ${VAR}. Written with no value, compose passes the variable only
    /// when the environment has it and drops the key otherwise, leaving the image honestly
    /// unstamped. Written as ${VAR}, an unset variable is forced to an empty STRING that overrides
    /// the Dockerfile's own ARG. A literal — which is what froze one estate on a August sha — is
    /// the failure this case exists for.
    /// </summary>
    [Fact]
    public void ComposeExample_TheIdentityArgs_CarryNoLiteralValue()
    {
        var lines = Read(ComposeExample).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var where = $"{ComposeExample}:{i + 1}";
            var trimmed = lines[i].Trim();
            if (!trimmed.StartsWith(BuildIdentity.RevisionVariable, StringComparison.Ordinal)
                && !trimmed.StartsWith(BuildIdentity.VersionVariable, StringComparison.Ordinal))
                continue;
            trimmed.Should().EndWith(":",
                $"{where} must pass the variable through, not pin a value — a literal here is how "
                + "an installation ends up reporting a build from a different month");
        }
    }

    [Fact]
    public void CliImage_DeclaresTheIdentityArgAndEnv()
    {
        // The local compose build has been passing these to an image that declared neither.
        Read(CliImage).Should()
            .Contain($"ARG {BuildIdentity.RevisionVariable}")
            .And.Contain($"ENV {BuildIdentity.RevisionVariable}=${BuildIdentity.RevisionVariable}");
    }

    private static int CountOf(string text, string needle) =>
        text.Split('\n').Count(l => l.Trim().StartsWith(needle, StringComparison.Ordinal));

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
