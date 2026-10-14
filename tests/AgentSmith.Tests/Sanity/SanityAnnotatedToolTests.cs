using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Sanity;

public sealed class SanityAnnotatedToolTests
{
    [Fact]
    public async Task SanityAnnotatedTool_InnerThrows_ExceptionPassesUnchanged()
    {
        var thrown = new InvalidOperationException("sandbox gone");
        var tool = (AIFunction)TestSanity.Wrapper()
            .Wrap([new SanityCalls.FixedResultTool("grep_in_tree", null, thrown)], SanityCalls.Pipeline()).Single();

        var act = async () => await tool.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(thrown);
    }

    [Fact]
    public async Task SanityAnnotatedTool_JsonStringResult_IsAnnotatedAsText()
    {
        var inner = AIFunctionFactory.Create(() => string.Empty, name: "grep_in_tree");
        var tool = (AIFunction)TestSanity.Wrapper()
            .Wrap([inner], SanityCalls.Pipeline(["src/Orders/Order.cs"])).Single();

        var result = await tool.InvokeAsync(
            new AIFunctionArguments { ["root"] = JsonDocument.Parse("\"src/Orders/Order.cs\"").RootElement },
            CancellationToken.None);

        result.Should().BeOfType<string>().Which.Should().StartWith("[framework] grep_in_tree found nothing in src/Orders/Order.cs");
    }

    [Fact]
    public async Task SanityAnnotatedTool_NothingFlagged_ResultIsByteIdentical()
    {
        var inner = AIFunctionFactory.Create(() => "src/Orders/Order.cs:3: class Order", name: "grep_in_tree");
        var direct = await inner.InvokeAsync(new AIFunctionArguments(), CancellationToken.None);
        var tool = (AIFunction)TestSanity.Wrapper().Wrap([inner], SanityCalls.Pipeline(["src/Orders/Order.cs"])).Single();

        var wrapped = await tool.InvokeAsync(new AIFunctionArguments { ["root"] = "src/Orders" }, CancellationToken.None);

        tool.Name.Should().Be("grep_in_tree");
        tool.JsonSchema.GetRawText().Should().Be(inner.JsonSchema.GetRawText());
        JsonSerializer.Serialize(wrapped).Should().Be(JsonSerializer.Serialize(direct));
        var plain = new object();
        (await SanityCalls.Call(SanityCalls.Pipeline(), "read_file", plain)).Should().BeSameAs(plain);
    }

    [Fact]
    public async Task ToolResultSanityScope_SpecChangesMidRun_NextCallReadsTheNewSpec()
    {
        var pipeline = SanityCalls.Pipeline(["src/Orders/Order.cs"]);
        var tool = (AIFunction)TestSanity.Wrapper()
            .Wrap([new SanityCalls.FixedResultTool("grep_in_file", string.Empty)], pipeline).Single();
        var args = new AIFunctionArguments { ["path"] = "src/Billing/Invoice.cs" };

        var before = await tool.InvokeAsync(args, CancellationToken.None);
        pipeline.Set(ContextKeys.CurrentSpec, SanityCalls.Spec(["src/Billing/Invoice.cs"]));
        var after = await tool.InvokeAsync(args, CancellationToken.None);

        before.Should().Be(string.Empty);
        after.Should().BeOfType<string>().Which.Should().Contain("[framework] grep_in_file found nothing in src/Billing/Invoice.cs");
    }
}
