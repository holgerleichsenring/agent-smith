using FluentAssertions;

namespace AgentSmith.Tests.Sanity;

public sealed class UncitedZeroHitRuleTests
{
    private static readonly string[] Cited =
    [
        "src/backend/Shop/Services/CartService.cs:12-30, src/backend/Shop/Orders",
    ];

    [Fact]
    public async Task UncitedZeroHit_GrepUnderACitedDirectory_IsFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(Cited), "grep_in_tree", "",
            ("pattern", "OrderPlaced"), ("root", "src/backend/Shop/Orders/Events"));

        text.Should().StartWith("[framework] grep_in_tree found nothing in src/backend/Shop/Orders/Events")
            .And.Contain("verify the pattern and the path before concluding absence");
    }

    [Fact]
    public async Task UncitedZeroHit_GrepInACitedFile_IsFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(Cited), "grep_in_file", "",
            ("path", "src/backend/Shop/Services/CartService.cs"), ("pattern", "Checkout"));

        text.Should().Contain("[framework] grep_in_file found nothing").And.Contain("CartService.cs");
    }

    [Fact]
    public async Task UncitedZeroHit_SearchOfTheRepositoryRoot_IsNotFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(Cited), "find_files", "", ("pattern", "*.cs"), ("root", "."));

        text.Should().BeEmpty();
    }

    [Fact]
    public async Task UncitedZeroHit_RepoPrefixedSearchOfABareCitedPath_IsFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(Cited), "grep_in_tree", "",
            ("pattern", "x"), ("root", "shop/src/backend/Shop/Orders"));

        text.Should().Contain("[framework] grep_in_tree found nothing in shop/src/backend/Shop/Orders");
    }

    [Fact]
    public async Task UncitedZeroHit_SearchOfAnAncestorOfACitedFile_IsNotFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(Cited), "grep_in_tree", "",
            ("pattern", "NewTypeName"), ("root", "src/backend"));

        text.Should().BeEmpty();
    }

    [Fact]
    public async Task UncitedZeroHit_WorkRootedSearchOfACitedPath_IsFlagged()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(Cited), "grep_in_file", "",
            ("path", "/work/src/backend/Shop/Services/CartService.cs"), ("pattern", "Checkout"));

        text.Should().Contain("[framework] grep_in_file found nothing in src/backend/Shop/Services/CartService.cs");
    }

    [Fact]
    public async Task UncitedZeroHit_StepTargetIsCited()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(targets: ["shop/web/app.ts"]), "grep_in_file", "",
            ("path", "./web/app.ts"), ("pattern", "x"));

        text.Should().Contain("[framework] grep_in_file found nothing");
    }

    [Fact]
    public async Task UncitedZeroHit_SearchWithHits_IsNotFlagged()
    {
        const string hits = "src/backend/Shop/Orders/Order.cs:3: class Order";

        var text = await SanityCalls.Text(SanityCalls.Pipeline(Cited), "grep_in_tree", hits,
            ("pattern", "Order"), ("root", "src/backend/Shop/Orders"));

        text.Should().Be(hits);
    }

    [Fact]
    public async Task UncitedZeroHit_NoCurrentSpec_IsOff()
    {
        var text = await SanityCalls.Text(SanityCalls.Pipeline(withSpec: false), "grep_in_tree", "",
            ("pattern", "Order"), ("root", "src/backend/Shop/Orders"));

        text.Should().BeEmpty();
    }
}
