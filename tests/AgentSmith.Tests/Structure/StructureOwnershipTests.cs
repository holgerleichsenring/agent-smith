using AgentSmith.Application.Services.Structure;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Structure;

/// <summary>
/// 2026-10-13-50c7: a path belongs to the context with the longest owned directory holding it;
/// on equal directories a workdir beats a source, and the smallest name wins among sources.
/// </summary>
public sealed class StructureOwnershipTests
{
    private readonly StructureOwnership _ownership = new();

    private IReadOnlyList<StructureClaim> Claims(params RemoteContextDiscovery[] contexts) => _ownership.Claims(contexts);

    private static RemoteContextDiscovery Context(string name, string workdir, params string[] sources) =>
        new(name, workdir, "csharp", Sources: sources);

    [Fact]
    public void StructureOwnership_SharedSource_OwnedByTheSmallestName()
    {
        var claims = Claims(Context("web", "src/Web", "src/Shared"), Context("api", "src/Api", "src/Shared"));

        _ownership.Owners(claims, "src/Shared/A.cs").Should().Equal("api");
        _ownership.Claimants(claims, "src/Shared/A.cs").Select(c => c.Context).Should().Equal("web", "api");
    }

    [Fact]
    public void StructureOwnership_WorkdirAndSourceOnOneDirectory_TheWorkdirOwns()
    {
        var claims = Claims(Context("a-lib-user", "src/Api", "src/Core"), Context("core", "src/Core"));

        _ownership.Owners(claims, "src/Core/A.cs").Should().Equal("core");
    }

    [Fact]
    public void StructureOwnership_SourceInsideAnotherWorkdir_TheLongerDirectoryOwns()
    {
        var claims = Claims(Context("root", "."), Context("api", "src/Api", "src/Core"));

        _ownership.Owners(claims, "src/Core/A.cs").Should().Equal("api");
        _ownership.Owners(claims, "src/Other/A.cs").Should().Equal("root");
    }

    [Fact]
    public void StructureOwnership_SharedWorkdir_EveryContextOwnsIt()
    {
        var claims = Claims(Context("one", "src"), Context("two", "src"));

        _ownership.Owners(claims, "src/A.cs").Should().Equal("one", "two");
    }

    [Fact]
    public void StructureOwnership_NoDirectoryHoldsThePath_NoOwner() =>
        _ownership.Owners(Claims(Context("api", "src/Api")), "docs/A.cs").Should().BeEmpty();

    [Fact]
    public void StructureOwnership_Claims_DropRootedEscapingRootAndOwnWorkdir() =>
        _ownership.Claims("api", "src/Api", ["/abs", "../x", ".", "./src/Api/", "src\\Core", "src/Core"])
            .Should().Equal(new StructureClaim("api", "src/Api", true), new StructureClaim("api", "src/Core", false));

    [Fact]
    public void StructureOwnership_RootedWorkdir_ClaimsNothing() =>
        _ownership.Claims("api", "/work/src", ["src/Core"]).Should().BeEmpty();
}
