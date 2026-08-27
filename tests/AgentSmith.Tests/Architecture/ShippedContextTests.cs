using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-25-5266: the context files this project ships pass the rules this project
/// enforces on everyone else's.
/// <para>
/// The write path judges what a model writes. These files are written by hand and
/// never go through it, so nothing held them to the schema (2026-08-25-056d) or the
/// image rule (2026-08-25-c9c7) — and both had drifted: the demo sample an evaluator
/// opens first named no image and declared a top-level <c>ci:</c> block the schema
/// does not know, so the worked example of a context was a file the writer would now
/// refuse to produce.
/// </para>
/// <para>
/// The rules used here are the PRODUCT'S, not a re-statement of them:
/// <see cref="ContextSchemaRule"/> over the schema embedded in the binary, and
/// <see cref="ContextStackImageRule"/> over the document the product's own reader
/// produces. A rule that changes changes this test with it.
/// </para>
/// </summary>
public sealed class ShippedContextTests
{
    private readonly ContextSchemaRule _schema = ContextGates.Rule();
    private readonly ContextStackImageRule _image = new();
    private readonly ContextYamlSerializer _reader = new(new ContextYamlBuilders());

    public static TheoryData<string> ShippedContexts => ShippedContextFiles.AsTheoryData();

    [Theory]
    [MemberData(nameof(ShippedContexts))]
    public void ShippedContext_EveryOne_PassesTheSchema(string path) =>
        _schema.Defect(YamlAsJson.Convert(File.ReadAllText(path))).Should().BeNull(
            "a context this project ships is an example of a context — it cannot fail "
            + $"the schema the product refuses a written context for ({path})");

    [Theory]
    [MemberData(nameof(ShippedContexts))]
    public void ShippedContext_EveryOne_NamesAnImageOrADomain(string path) =>
        _image.Defect(Read(path)).Should().BeNull(
            "a shipped context without an image resolves through the language fallback "
            + $"table the rule exists to retire ({path})");

    /// <summary>
    /// The anti-drift assertion: the enumeration is a discovery, so the two files this
    /// phase fixed cannot be quietly dropped from it and a third one added later is
    /// covered without anybody editing this test.
    /// </summary>
    [Fact]
    public void ShippedContext_EveryOneInTheRepo_IsCoveredByThisRule()
    {
        var covered = ShippedContextFiles.All;

        covered.Should()
            .Contain(Path.GetFullPath(ContextSchemaFile.ContextPath),
                "this repository's own context is the first one the rules have to describe")
            .And.Contain(Path.GetFullPath(ContextSchemaFile.TemplatePath),
                "the template is copied into every new project");
        covered.Should().ContainSingle(
            path => path.Contains("DemoSampleProject", StringComparison.Ordinal),
            "the demo sample is the context an evaluator opens first");
        covered.Should().NotContain(
            path => path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal),
            "fixtures are inputs to the product, several deliberately invalid — they do not ship");
    }

    // The image rule judges the typed document, and the product's own reader is what
    // turns a file on disk into one. Going through it means this test sees exactly the
    // stack.image and meta.domain the sandbox will see — including nothing at all, when
    // the reader drops a key it does not know.
    private ContextYamlDocument Read(string path)
    {
        var parsed = _reader.Parse(File.ReadAllText(path));
        parsed.ErrorReason.Should().BeNull($"a shipped context must parse ({path})");
        parsed.Summary.Should().NotBeNull($"a shipped context must carry a meta block ({path})");
        return new ContextYamlDocument(
            new ContextYamlMeta(parsed.Summary!.Workdir, Domain: parsed.Summary.Domain),
            Stack(parsed.Summary));
    }

    // No language and no image is how a file with no stack block at all reaches here;
    // reporting it as a missing block rather than a missing field names the real defect.
    private static ContextYamlStack? Stack(ContextYamlSummary summary) =>
        summary.Language is null && summary.Image is null
            ? null
            : new ContextYamlStack(Lang: summary.Language, Image: summary.Image);
}
