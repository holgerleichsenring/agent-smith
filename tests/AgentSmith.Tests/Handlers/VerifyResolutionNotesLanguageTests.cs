using AgentSmith.Application.Services.Handlers;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-03-ee12: the report a repository gets when nothing named a command says which
/// sources were consulted, and those sources are the same for every stack. The text used
/// to name a .NET entry-point search, which told a python, rust or lua repository about a
/// step that was never going to run for it.
/// </summary>
public sealed class VerifyResolutionNotesLanguageTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("python")]
    [InlineData("rust")]
    public void NothingDeclared_NamesNoLanguageSpecificSource(string language)
    {
        var notes = new VerifyResolutionNotes();

        notes.NothingDeclared("server", language);

        var report = notes.Searched.Should().ContainSingle().Subject;
        report.Should().Contain("context.yaml verify block")
            .And.Contain("ci.build_command")
            .And.Contain(language, "the report still says what the repository IS");
        report.Should().NotContain("entry point",
            "the sources consulted are the same for every stack");
    }
}
