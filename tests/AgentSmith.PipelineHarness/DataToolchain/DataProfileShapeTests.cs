using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.PipelineHarness.DataToolchain;

/// <summary>
/// p0513: the profile is one flat list, and the measurement is PER SHAPE. These
/// resolve the packaged profile against the very fixture trees p0505 measured, so
/// a repository only ever runs commands measured green on a repository of its own
/// shape — and a repository carrying neither shape's files runs none of them.
/// </summary>
public sealed class DataProfileShapeTests : IDisposable
{
    private const string Domain = "dbt-databricks";
    private const string CleanVariant = "clean";

    private readonly PackagedProfiles _profiles = new();
    private readonly MeasuredCommandsSource _source = new();
    private readonly MeasuredCommandGate _gate = new();

    // 2026-08-28-3302: one guard, and it asserts. This used to arm at the pin and
    // return null below it, and every caller then returned early — an assertion that
    // does not run reads exactly like one that holds.
    private DomainProfile Profile()
    {
        var profile = _profiles.Find(Domain);
        profile.Should().NotBeNull(
            $"the embedded pin is {_profiles.Pin} and profiles ship from "
            + $"{PackagedProfiles.ProfilesFrom} — a pin below that carries no profile to police");
        return profile!;
    }

    // The presence condition is answered by the real clean fixture of one shape:
    // the sandbox path /work/<relative> maps onto that tree.
    private async Task<IReadOnlyList<string>> ResolveAsync(DomainProfile profile, string shape)
    {
        var root = Path.Combine(_source.FixturesDirectory(), shape, CleanVariant);
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string p, CancellationToken _) => Exists(root, p));
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(reader.Object);
        var sut = new VerifyStageResolver(
            new DotnetEntryPointDiscovery(factory.Object, NullLogger<DotnetEntryPointDiscovery>.Instance),
            new ProfileCommandPresence(factory.Object, NullLogger<ProfileCommandPresence>.Instance),
            NullLogger<VerifyStageResolver>.Instance);
        var map = new ProjectMap("python", [], [], [], [], new Conventions(null, null, null),
            new CiConfig(false, null, null, null));

        return [.. (await sut.ResolveAsync(
            "data", map, new Mock<ISandbox>().Object, "/work",
            [new DomainProfileStages(profile, "/work")], [], CancellationToken.None))
            .Select(s => s.Command)];
    }

    private static bool Exists(string root, string sandboxPath)
    {
        var relative = sandboxPath.TrimStart('/');
        relative = relative.StartsWith("work/", StringComparison.Ordinal) ? relative[5..] : relative;
        var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(target) || Directory.Exists(target);
    }

    [Fact]
    public void Profile_Data_TheDbtPairIsConditionedOnTheProjectFile()
    {
        var profile = Profile();

        profile.Verify.Should().Contain(
            c => c.Command.StartsWith("dbt ", StringComparison.Ordinal)
                && c.WhenPresent == "dbt_project.yml",
            "the dbt commands were measured on dbt-bearing shapes only");
    }

    [Fact]
    public void Profile_Data_TheLinterIsConditionedOnTheModelDirectory()
    {
        var profile = Profile();

        profile.Verify.Should().Contain(
            c => c.Command.StartsWith("sqlfluff ", StringComparison.Ordinal)
                && c.WhenPresent == "models",
            "the linter lints models/, and was never measured on a shape without one");
    }

    [Fact]
    public async Task DbtOnlyShape_ResolvesTheDbtPairAndTheLinterAndNothingElse()
    {
        var profile = Profile();

        var commands = await ResolveAsync(profile, "dbt");

        commands.Should().HaveCount(2).And.BeSubsetOf(_gate.DeclarableOn(_source.Load(), "dbt"),
            "a dbt repository runs exactly what the dbt shape measured as declarable");
        commands.Should().Contain(c => c.StartsWith("dbt ", StringComparison.Ordinal));
        commands.Should().Contain(c => c.StartsWith("sqlfluff ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BundleOnlyShape_ResolvesNoCommandsFromThisProfile()
    {
        var profile = Profile();

        (await ResolveAsync(profile, "bundle")).Should().BeEmpty(
            "the bundle shape has no dbt or sqlfluff row at all — running either would be a "
            + "red this profile has no evidence for, and it would hide the gates behind it");
    }

    public void Dispose() => _profiles.Dispose();
}
