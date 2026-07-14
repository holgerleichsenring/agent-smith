using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.SkillsPackaging;
using FluentAssertions;

namespace AgentSmith.Tests.SkillsPackaging;

/// <summary>
/// p0325: the build-time guard against the v3.16.0 silent-drop incident — a
/// master SKILL.md description over the runtime loader limit (200 chars) must
/// fail PACKAGING, not silently vanish at runtime. The validator is exercised
/// directly here; the MSBuild EmbedSkillsCatalog step invokes the same class
/// via the AgentSmith.SkillsPackaging tool.
/// </summary>
public sealed class MasterDescriptionValidatorTests
{
    [Fact]
    public void Build_MasterDescriptionOverLimit_FailsPackaging()
    {
        var overLimit = new string('x', MasterDescriptionValidator.MaxDescriptionChars + 14);
        using var tarball = BuildTarball(("skills/_masters/big-master/SKILL.md", MasterSkillMd(overLimit)));

        var violations = new MasterDescriptionValidator().Validate(tarball);

        violations.Should().ContainSingle();
        violations[0].Master.Should().Be("big-master", "the build error must name the offending master");
        violations[0].Reason.Should().Contain("214").And.Contain("200");
    }

    [Fact]
    public void Validate_DescriptionsWithinLimit_ReportsNoViolations()
    {
        using var tarball = BuildTarball(
            ("skills/_masters/good-master/SKILL.md", MasterSkillMd("A perfectly sized description.")),
            ("skills/coding/some-skill/SKILL.md", MasterSkillMd("Non-master entries are not this tool's concern.")));

        new MasterDescriptionValidator().Validate(tarball).Should().BeEmpty();
    }

    [Fact]
    public void Validate_MasterWithoutDescription_FailsPackaging()
    {
        using var tarball = BuildTarball(
            ("skills/_masters/mute-master/SKILL.md", "---\nname: mute-master\nrole: master\n---\nbody"));

        var violations = new MasterDescriptionValidator().Validate(tarball);

        violations.Should().ContainSingle().Which.Reason.Should().Contain("missing or empty");
    }

    [Fact]
    public void Validate_TarballWithoutMasters_FailsPackaging()
    {
        using var tarball = BuildTarball(("patterns/auth.yaml", "patterns: []"));

        var violations = new MasterDescriptionValidator().Validate(tarball);

        violations.Should().ContainSingle()
            .Which.Reason.Should().Contain("no skills/_masters",
                "an embedded tarball with no masters would brick every pipeline at runtime");
    }

    [Fact]
    public void Validate_EmbeddedReleaseTarball_Passes()
    {
        // Cross-check: the tarball actually baked into this build must satisfy
        // the same rule the MSBuild step enforces.
        using var tarball = new EmbeddedSkillsCatalog().Open();

        new MasterDescriptionValidator().Validate(tarball).Should().BeEmpty();
    }

    private static string MasterSkillMd(string description) =>
        $"---\nname: m\ndescription: \"{description}\"\nrole: master\n---\nbody";

    private static Stream BuildTarball(params (string Path, string Content)[] entries)
    {
        var buffer = new MemoryStream();
        using (var gz = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
        using (var tar = new TarWriter(gz))
        {
            foreach (var (path, content) in entries)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                };
                tar.WriteEntry(entry);
            }
        }

        buffer.Position = 0;
        return buffer;
    }
}
