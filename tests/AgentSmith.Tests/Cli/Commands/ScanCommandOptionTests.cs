using System.CommandLine;
using System.CommandLine.Parsing;
using AgentSmith.Cli.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Cli.Commands;

/// <summary>
/// Options the scan and legal commands accept are options something reads: no --pr that
/// nothing consumed, no `file` format no strategy renders, no --source-url dropped for want
/// of a type.
/// </summary>
public sealed class ScanCommandOptionTests
{
    private readonly Option<string> _config = new("--config", () => "config.yml");
    private readonly Option<bool> _verbose = new("--verbose");

    [Fact]
    public void SecurityScanCommand_HasNoPrOption()
    {
        var command = SecurityScanCommand.Create(_config, _verbose);

        command.Options.Should().NotContain(o => o.Name == "pr");
        command.Parse("--agent a --pr 42").Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void LegalCommand_UnknownOutputFormat_Rejected()
    {
        var command = LegalCommand.Create(_config, _verbose);

        command.Parse("--source doc.pdf --output file").Errors.Should().NotBeEmpty();
        command.Parse("--source doc.pdf --output markdown --output-dir out").Errors.Should().BeEmpty();
    }

    [Fact]
    public void SourceOptions_SourceUrlWithoutType_Rejected()
    {
        var command = ApiScanCommand.Create(_config, _verbose);
        const string target = "--swagger s.json --target https://api.example.test";

        var withoutType = command.Parse($"{target} --source-url https://github.com/x/y");
        var withType = command.Parse($"{target} --source-url https://github.com/x/y --source-type github");

        withoutType.Errors.Should().ContainSingle(e => e.Message.Contains("--source-type"));
        withType.Errors.Should().BeEmpty();
    }
}
