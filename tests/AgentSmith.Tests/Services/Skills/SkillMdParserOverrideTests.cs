using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Skills;

public sealed class SkillMdParserOverrideTests : IDisposable
{
    private readonly string _skillDir;

    public SkillMdParserOverrideTests()
    {
        _skillDir = Path.Combine(Path.GetTempPath(), "agentsmith-parser-override-" + Guid.NewGuid());
        Directory.CreateDirectory(_skillDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_skillDir)) Directory.Delete(_skillDir, recursive: true);
    }

    [Fact]
    public void Parse_OverrideWithMismatchedName_RejectsWithClearError()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), """
            ---
            name: architect
            description: x
            role: producer
            output_schema: plan
            activates_when: "true"
            ---
            base body
            """);
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.openai.md"), """
            ---
            name: architect-openai
            role: producer
            ---
            override body
            """);
        var parser = NewParser("openai");

        var act = () => parser.Parse(_skillDir);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*name='architect-openai'*name='architect'*Names must match*");
    }

    [Fact]
    public void Parse_OverrideWithMismatchedRole_RejectsWithClearError()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), """
            ---
            name: architect
            description: x
            role: producer
            output_schema: plan
            activates_when: "true"
            ---
            base body
            """);
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.openai.md"), """
            ---
            name: architect
            role: judge
            ---
            override body
            """);
        var parser = NewParser("openai");

        var act = () => parser.Parse(_skillDir);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*role='judge'*role='producer'*Roles must match*");
    }

    [Fact]
    public void Parse_OverrideLoaded_LogsInfo()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), """
            ---
            name: architect
            description: base
            role: producer
            output_schema: plan
            activates_when: "true"
            ---
            base body
            """);
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.openai.md"), """
            ---
            name: architect
            description: tuned for openai
            ---
            override body
            """);
        var loggerMock = new Mock<ILogger>();
        var parser = NewParser("openai", loggerMock.Object);

        var role = parser.Parse(_skillDir);

        role.Should().NotBeNull();
        loggerMock.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("override loaded")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public void Parse_OverrideOmitsField_InheritsFromBaseFrontmatter()
    {
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.md"), """
            ---
            name: architect
            description: base description
            emoji: 🏛️
            role: producer
            output_schema: plan
            activates_when: "true"
            ---
            base body
            """);
        File.WriteAllText(Path.Combine(_skillDir, "SKILL.openai.md"), """
            ---
            name: architect
            description: openai description
            ---
            override body
            """);
        var parser = NewParser("openai");

        var role = parser.Parse(_skillDir);

        role.Should().NotBeNull();
        role!.Description.Should().Be("openai description");
        role.Emoji.Should().Be("🏛️"); // inherited from base
        role.Rules.Should().Be("override body");
    }

    private SkillMdParser NewParser(string activeProvider, ILogger? logger = null)
    {
        var providerMock = new Mock<IActiveProviderResolver>();
        providerMock.Setup(x => x.GetActiveProvider()).Returns(activeProvider);
        var resolver = new ProviderOverrideResolver(providerMock.Object);
        return new SkillMdParser(resolver, logger ?? NullLogger.Instance);
    }
}
