using AgentSmith.Infrastructure.Core.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class LlmResponseHelperTests
{
    [Fact]
    public void StripCodeFences_PlainYaml_ReturnsUnchanged()
    {
        var yaml = "meta:\n  project: test";

        LlmResponseHelper.StripCodeFences(yaml).Should().Be(yaml);
    }

    [Fact]
    public void StripCodeFences_YamlFences_StripsCodeBlock()
    {
        var input = "```yaml\nmeta:\n  project: test\n```";

        LlmResponseHelper.StripCodeFences(input).Should().Be("meta:\n  project: test");
    }

    [Fact]
    public void StripCodeFences_GenericFences_StripsCodeBlock()
    {
        var input = "```\nmeta:\n  project: test\n```";

        LlmResponseHelper.StripCodeFences(input).Should().Be("meta:\n  project: test");
    }

    [Fact]
    public void StripCodeFences_WhitespaceAroundFences_Trims()
    {
        var input = "  ```yaml\nkey: value\n```  ";

        LlmResponseHelper.StripCodeFences(input).Should().Be("key: value");
    }

    [Fact]
    public void IsValidYaml_ValidYaml_ReturnsTrue()
    {
        var yaml = "modules:\n  - name: Core\n    path: src/Core";

        LlmResponseHelper.IsValidYaml(yaml).Should().BeTrue();
    }

    [Fact]
    public void IsValidYaml_InvalidYaml_ReturnsFalse()
    {
        var yaml = "{{invalid: [yaml";

        LlmResponseHelper.IsValidYaml(yaml).Should().BeFalse();
    }

    [Fact]
    public void IsValidYaml_EmptyString_ReturnsFalse()
    {
        LlmResponseHelper.IsValidYaml("").Should().BeFalse();
        LlmResponseHelper.IsValidYaml("  ").Should().BeFalse();
    }

    [Fact]
    public void IsValidYaml_SimpleKeyValue_ReturnsTrue()
    {
        LlmResponseHelper.IsValidYaml("key: value").Should().BeTrue();
    }
}
