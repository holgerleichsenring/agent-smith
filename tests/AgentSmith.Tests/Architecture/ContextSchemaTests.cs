using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-08-25-056d: the context schema describes the files it validates.
/// <para>
/// It had stopped: this repository's own context produced 632 errors against it and the
/// template handed to every new project failed on <c>meta.workdir</c>, the one field the
/// runtime genuinely enforces. A schema nothing is measured against rots silently — three
/// separate fields (p0265's <c>stack.image</c>, p0272's <c>prerequisites</c>, and
/// <c>stack.frameworks</c>, on the typed writer since p0193) shipped without the schema
/// ever learning about them, so a context naming its frameworks failed the very schema
/// its own writer emits.
/// </para>
/// <para>
/// The two shipped files ARE the acceptance criteria; the writer-shaped checks are what
/// stops the drift coming back.
/// </para>
/// </summary>
public sealed class ContextSchemaTests
{
    private readonly ContextYamlSerializer _writer = new(new ContextYamlBuilders());

    [Fact]
    public void Schema_ThisRepositorysOwnContext_Validates() =>
        ContextSchemaFile.ValidateFile(ContextSchemaFile.ContextPath).Should().BeEmpty(
            "the repository that ships the schema is the first file it has to describe");

    [Fact]
    public void Schema_TheMethodologyTemplate_Validates() =>
        ContextSchemaFile.ValidateFile(ContextSchemaFile.TemplatePath).Should().BeEmpty(
            "the template is copied into every new project — it cannot fail its own schema");

    /// <summary>
    /// The guard against the next <c>frameworks</c>: a field added to the typed document
    /// but not to the schema turns every context that uses it into an invalid one, because
    /// every block is <c>additionalProperties: false</c>.
    /// </summary>
    [Theory]
    [InlineData(typeof(ContextYamlDocument), "")]
    [InlineData(typeof(ContextYamlMeta), "meta")]
    [InlineData(typeof(ContextYamlStack), "stack")]
    [InlineData(typeof(ContextYamlStackResources), "stack.resources")]
    [InlineData(typeof(ContextYamlRegistryAuth), "registry_auth")]
    [InlineData(typeof(ContextYamlVerifyDerivation), "verify_derived_from")]
    public void Schema_EveryBlockTheWriterCanEmit_IsDeclared(Type written, string blockPath)
    {
        var declared = ContextSchemaFile.DeclaredKeys(blockPath);

        var undeclared = written.GetProperties()
            .Select(property => UnderscoredNamingConvention.Instance.Apply(property.Name))
            .Where(key => !declared.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        undeclared.Should().BeEmpty(
            $"{written.Name} emits these keys and the schema rejects what it does not "
            + $"declare — add them under '{blockPath}'.\n  " + string.Join("\n  ", undeclared));
    }

    /// <summary>
    /// The other direction: a required field the writer has no way to produce would refuse
    /// every freshly bootstrapped context. The smallest document the writer can emit is
    /// meta.workdir and nothing else, so that document has to be a valid one.
    /// </summary>
    [Fact]
    public void Schema_EveryRequiredField_CanBeExpressedByTheWriter()
    {
        var yaml = _writer.Serialize(new ContextYamlDocument(new ContextYamlMeta(Workdir: ".")));

        ContextSchemaFile.Validate(yaml).Should().BeEmpty(
            "a bootstrapped context must not be asked for a block no writer can produce");
    }

    /// <summary>
    /// 2026-08-26-04b6: the fullest document the writer can still emit. Its reading fields
    /// (project, version, runtime, frameworks, infra, testing, sdks) left the typed record
    /// in that phase; the schema still declares them, deprecated, so nothing written before
    /// is invalidated.
    /// </summary>
    [Fact]
    public void Schema_TheFullestContextTheWriterEmits_Validates()
    {
        var yaml = _writer.Serialize(new ContextYamlDocument(
            new ContextYamlMeta(
                Workdir: ".", Type: ["api", "worker"], Purpose: "Sample context for the schema."),
            new ContextYamlStack(Lang: "C#", Image: "mcr.microsoft.com/dotnet/sdk:8.0")));

        ContextSchemaFile.Validate(yaml).Should().BeEmpty(
            "meta.type has always been a list of archetypes — the shape comes from the "
            + "writer, not from a guess");
    }
}
