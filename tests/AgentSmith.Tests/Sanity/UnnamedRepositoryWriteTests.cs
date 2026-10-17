using FluentAssertions;

namespace AgentSmith.Tests.Sanity;

public sealed class UnnamedRepositoryWriteRuleTests
{
    private static readonly Dictionary<string, string> TwoRepositories = new()
    {
        ["shop-dotnet"] = "shop",
        ["shop-node"] = "shop",
        ["billing"] = "billing",
    };

    [Fact]
    public async Task UnnamedRepositoryWrite_WriteToARepositoryTheSpecDoesNotName_IsFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "write_file", "Wrote 12 bytes to src/Invoice.cs", ("path", "billing/src/Invoice.cs"), ("content", "x"));

        text.Should().StartWith("Wrote 12 bytes to src/Invoice.cs\n[framework] write_file wrote src/Invoice.cs into repository 'billing'")
            .And.Contain("which this spec does not name (it names shop)");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_SingleRepositoryBarePathOfAnUnnamedRepository_IsFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["billing"]), "edit", "Edited src/a.cs", ("path", "src/a.cs"));

        text.Should().Contain("[framework] edit wrote src/a.cs into repository 'shop'");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_SpecNamesNoRepositories_IsOff()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(map: TwoRepositories),
            "write_file", "Wrote", ("path", "billing/src/Invoice.cs"));

        text.Should().Be("Wrote");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_FailedWrite_IsNotFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "write_file", "Error: write refused", ("path", "billing/src/Invoice.cs"));

        text.Should().Be("Error: write refused");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_MultiEditDryRun_IsNotFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "multi_edit", "dry_run: would apply 2 edit(s)", ("path", "billing/src/Invoice.cs"), ("dry_run", true));

        text.Should().Be("dry_run: would apply 2 edit(s)");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_MultiEditErrorInEdit_IsNotFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "multi_edit", "Error in edit #2: old_string not found", ("path", "billing/src/Invoice.cs"));

        text.Should().Be("Error in edit #2: old_string not found");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_UnprefixedPathOnAMultiRepositoryRun_IsNotJudged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "write_file", "Wrote", ("path", "src/Invoice.cs"));

        text.Should().Be("Wrote");
    }

    [Fact]
    public async Task UnnamedRepositoryWrite_ToolchainKeyOfANamedRepository_IsNotFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(repos: ["shop"], map: TwoRepositories),
            "edit", "Edited", ("path", "shop-node/web/app.ts"));

        text.Should().Be("Edited");
    }
}
