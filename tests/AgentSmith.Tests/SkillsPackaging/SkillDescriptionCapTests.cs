using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.SkillsPackaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SkillsPackaging;

/// <summary>
/// p0518: the two gates used to declare the cap apart, so a description could pass
/// one and fail the other. These tests judge BEHAVIOUR, not the declaration: each
/// gate is probed for the number it enforces, on the same SKILL.md text.
/// </summary>
public sealed class SkillDescriptionCapTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "agentsmith-desccap-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Cap_ThePackagingValidatorAndTheParser_UseTheSameNumber()
    {
        var packaging = LargestAccepted(PackagingRejection);
        var parser = LargestAccepted(ParserRejection);

        parser.Should().Be(packaging, "one cap, or a description passes one gate and fails the other");
        packaging.Should().Be(MasterDescriptionValidator.MaxDescriptionChars);
    }

    [Fact]
    public void Cap_ADescriptionAtTheCap_PassesBothValidators()
    {
        var skillMd = MasterSkillMd(new string('x', MasterDescriptionValidator.MaxDescriptionChars));

        PackagingRejection(skillMd).Should().BeNull();
        ParserRejection(skillMd).Should().BeNull();
    }

    [Fact]
    public void Cap_ADescriptionOneOverTheCap_FailsBothValidators()
    {
        var over = MasterDescriptionValidator.MaxDescriptionChars + 1;
        var skillMd = MasterSkillMd(new string('x', over));

        PackagingRejection(skillMd).Should().Contain(over.ToString());
        ParserRejection(skillMd).Should().Contain(over.ToString());
    }

    [Fact]
    public void Cap_ABlockScalarDescription_IsTreatedTheSameByBoth()
    {
        // The shell gate in the catalog repository refuses a block scalar because it
        // cannot measure one as text. Both binary gates now refuse it for the same
        // reason, so a fixture cannot be green on one side and red on the other.
        const string skillMd = "---\nname: m\ndescription: |\n  a short description\nrole: master\n---\nbody";

        PackagingRejection(skillMd).Should().Contain("single-line scalar");
        ParserRejection(skillMd).Should().Contain("single-line scalar");
    }

    /// <summary>Binary-searches the longest description the gate accepts.</summary>
    private int LargestAccepted(Func<string, string?> gate)
    {
        int low = 1, high = 1000;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (gate(MasterSkillMd(new string('x', middle))) is null) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    private string? PackagingRejection(string skillMd)
    {
        using var tarball = BuildTarball("skills/_masters/probe/SKILL.md", skillMd);
        return new MasterDescriptionValidator().Validate(tarball).SingleOrDefault()?.Reason;
    }

    private string? ParserRejection(string skillMd)
    {
        var dir = Path.Combine(_tempDir, "probe-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), skillMd);
        var parser = new SkillMdParser(
            new ProviderOverrideResolver(new ActiveProviderResolver(new AgentSmithConfig())),
            NullLogger.Instance);
        try
        {
            parser.Parse(dir);
            return null;
        }
        catch (SkillFormatException ex)
        {
            return ex.Message;
        }
    }

    private static string MasterSkillMd(string description) =>
        $"---\nname: m\ndescription: \"{description}\"\nrole: master\n---\nbody";

    private static Stream BuildTarball(string path, string content)
    {
        var buffer = new MemoryStream();
        using (var gz = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
        using (var tar = new TarWriter(gz))
            tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, path)
                { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)) });
        buffer.Position = 0;
        return buffer;
    }
}
