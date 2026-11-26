using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>2026-11-26-462aa: done.md's lines, the union attributes, and state.done read out of a context.yaml.</summary>
public sealed class ContextDoneIndexTests
{
    [Fact]
    public void Upsert_AnAbsentFile_StartsWithTheHeaderAndOneLine()
    {
        var text = ContextDoneIndex.Upsert(null, "s1", "one");

        text.Should().Be(ContextDoneIndex.Header + "\n- s1: one\n");
    }

    [Fact]
    public void Upsert_AnEmptyFile_StartsWithTheHeader()
    {
        ContextDoneIndex.Upsert("\n", "s1", "one").Should().Be(ContextDoneIndex.Header + "\n- s1: one\n");
    }

    [Fact]
    public void Upsert_ANewId_GoesAboveTheFirstEntry()
    {
        var text = ContextDoneIndex.Upsert(ContextDoneIndex.Upsert(null, "s1", "one"), "s2", "two");

        ContextDoneIndex.Entries(text).Select(e => e.Key).Should().Equal("s2", "s1");
    }

    [Fact]
    public void Upsert_AKnownId_ReplacesItsLineInPlaceAndDropsATwin()
    {
        var unioned = ContextDoneIndex.Header + "\n- s2: two\n- s1: one\n- s1: one, the other side\n";

        var text = ContextDoneIndex.Upsert(unioned, "s1", "one, again");

        ContextDoneIndex.Entries(text).Should().Equal(
            new KeyValuePair<string, string>("s2", "two"), new KeyValuePair<string, string>("s1", "one, again"));
    }

    [Fact]
    public void Append_OlderEntries_GoBelowAndSkipKnownIds()
    {
        var text = ContextDoneIndex.Append(ContextDoneIndex.Upsert(null, "s2", "two"),
            [new("s2", "two, older copy"), new("s1", "one")]);

        ContextDoneIndex.Entries(text).Select(e => e.Value).Should().Equal("two", "one");
    }

    [Fact]
    public void Entries_ProseLines_AreNotEntries()
    {
        ContextDoneIndex.Entries(ContextDoneIndex.Header).Should().BeEmpty();
    }

    [Fact]
    public void Ensure_AnAbsentFile_GetsBothLines()
    {
        IndexMergeAttributes.Ensure(null).Should().Be(
            ".agentsmith/contexts/*/done.md merge=union\n.agentsmith/memory/MEMORY.md merge=union\n");
    }

    [Fact]
    public void Ensure_AFileWithBothLines_IsUnchanged()
    {
        IndexMergeAttributes.Ensure("* text=auto\n" + IndexMergeAttributes.Ensure(null)).Should().BeNull();
    }

    [Fact]
    public void Extract_AContextWithoutStateDone_FindsNothing()
    {
        var extracted = Codec().Extract("meta:\n  workdir: \".\"\nstate:\n  active: {}\n");

        extracted.Yaml.Should().BeNull();
        extracted.Error.Should().BeNull();
        extracted.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Extract_AnInlineEmptyDone_IsRemoved()
    {
        var extracted = Codec().Extract("state:\n  done: {}\n  active: {}\n");

        extracted.Yaml.Should().Be("state:\n  active: {}\n");
        extracted.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Extract_AMultiLineValue_BecomesOneLine()
    {
        var extracted = Codec().Extract("state:\n  done:\n    p1: >-\n      folded\n      text\n    p2: \"a\\nb\"\n");

        extracted.Entries.Should().Equal(new KeyValuePair<string, string>("p1", "folded text"),
            new KeyValuePair<string, string>("p2", "a b"));
        extracted.Yaml.Should().Be(string.Empty);
    }

    private static ContextYamlStateDoneCodec Codec() => new(new ContextYamlBuilders());
}
