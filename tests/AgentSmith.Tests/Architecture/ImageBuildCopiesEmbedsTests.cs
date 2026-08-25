using System.IO.Enumeration;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-25-2103: every methodology file the binary EMBEDS is a file the image build
/// COPIES.
/// <para>
/// The two lists are written in different files by different hands. c9c7 added
/// <c>.agentsmith/context.schema.json</c> to the project's embedded resources; the
/// Dockerfiles still copied one named schema at that root, so the compiler asked for a
/// file that was not in the build context and every image failed at publish with CS1566
/// — while the checkout built green, the suite passed and the phase gate said yes,
/// because on a checkout the file is simply there.
/// </para>
/// <para>
/// The rule judges the PAIR rather than the shape of either side: a Dockerfile that
/// publishes a project must copy every <c>.agentsmith</c> path that project embeds. A
/// broader COPY satisfies it, and so does a narrower one that happens to name the file —
/// what it refuses is an embed no COPY reaches, which is the only thing that breaks.
/// </para>
/// </summary>
public sealed class ImageBuildCopiesEmbedsTests
{
    // <EmbeddedResource Include="..\..\..\.agentsmith\context.schema.json" Link="…" />
    private static readonly Regex Embed = new(
        @"<EmbeddedResource\s+Include\s*=\s*""(?<path>[^""]*[\\/]\.agentsmith[\\/][^""]+)""",
        RegexOptions.Compiled);

    // COPY <source> <destination> — the source is what has to reach the build context.
    private static readonly Regex Copy = new(
        @"^\s*COPY\s+(?<source>\.agentsmith[^\s]*)\s", RegexOptions.Compiled | RegexOptions.Multiline);

    public static TheoryData<string, string> EmbedAndDockerfile()
    {
        var data = new TheoryData<string, string>();
        foreach (var project in ProjectsWithMethodologyEmbeds())
            foreach (var dockerfile in DockerfilesPublishing(project.Path))
                foreach (var embedded in project.Embeds)
                    data.Add(embedded, dockerfile);
        return data;
    }

    [Theory]
    [MemberData(nameof(EmbedAndDockerfile))]
    public void ImageBuild_EveryEmbeddedMethodologyFile_IsCopiedByEveryDockerfileThatPublishesIt(
        string embedded, string dockerfile)
    {
        var sources = Copy.Matches(File.ReadAllText(dockerfile))
            .Select(m => m.Groups["source"].Value)
            .ToList();
        sources.Any(source => Covers(source, embedded)).Should().BeTrue(
            $"'{embedded}' is compiled into the binary this image publishes, so it must be in "
            + $"the build context — {Path.GetFileName(Path.GetDirectoryName(dockerfile))}'s "
            + $"Dockerfile copies only [{string.Join(", ", sources)}], and a missing embed fails "
            + "the publish with CS1566 long after the checkout built green");
    }

    /// <summary>
    /// The anti-vacuum assertion: this rule is two discoveries, and either coming back
    /// empty would turn every case above into a pass that proves nothing.
    /// </summary>
    [Fact]
    public void ImageBuild_TheEnumeration_FindsBothTheEmbedsAndTheDockerfiles()
    {
        var projects = ProjectsWithMethodologyEmbeds();
        projects.Should().NotBeEmpty("a project embeds the methodology's schemas");
        projects.SelectMany(p => DockerfilesPublishing(p.Path))
            .Should().NotBeEmpty("images are built from those projects");
    }

    private static IReadOnlyList<(string Path, IReadOnlyList<string> Embeds)>
        ProjectsWithMethodologyEmbeds() =>
        Directory.EnumerateFiles(ArchitectureSources.BackendRoot, "*.csproj",
                SearchOption.AllDirectories)
            .Select(path => (Path: path, Embeds: MethodologyEmbedsIn(path)))
            .Where(p => p.Embeds.Count > 0)
            .OrderBy(p => p.Path, StringComparer.Ordinal)
            .ToList();

    // The include is relative to the project directory and written with backslashes;
    // what the Dockerfile copies is relative to the repository root with forward ones.
    private static IReadOnlyList<string> MethodologyEmbedsIn(string csproj) =>
        Embed.Matches(File.ReadAllText(csproj))
            .Select(m => m.Groups["path"].Value.Replace('\\', '/'))
            .Select(p => p[p.IndexOf(".agentsmith/", StringComparison.Ordinal)..])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    // A Dockerfile publishes a project when it names that project's directory in a
    // restore or publish line — which is how the image says what it compiles.
    private static IEnumerable<string> DockerfilesPublishing(string csproj)
    {
        var project = Path.GetFileNameWithoutExtension(csproj);
        return Directory.EnumerateFiles(ArchitectureSources.BackendRoot, "Dockerfile",
                SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains($"{project}.csproj", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);
    }

    // Docker's COPY: a trailing slash is a directory and takes everything under it;
    // a wildcard matches inside ONE path segment, never across separators.
    private static bool Covers(string source, string required)
    {
        if (source.EndsWith('/')) return required.StartsWith(source, StringComparison.Ordinal);
        var pattern = source.Split('/');
        var path = required.Split('/');
        if (pattern.Length != path.Length) return false;
        return !pattern.Where((segment, i) =>
            !FileSystemName.MatchesSimpleExpression(segment, path[i], ignoreCase: false)).Any();
    }
}
