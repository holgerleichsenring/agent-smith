using AgentSmith.Application.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-14-9f80b: a stored spec record is read as it was written. A draft keyed with the old noun is
/// still refused (<see cref="SpecDraftReaderSpecKeyTests"/>); a RECORD keyed so was written before the
/// noun changed and reads under the id its row names.
/// </summary>
public sealed class SpecRecordReaderTests
{
    private readonly SpecRecordReader _reader = new(new SpecDraftReader());

    [Fact]
    public void SpecRecordReader_RecordFromBeforeTheRename_ReadsUnderTheRowsId()
    {
        var draft = _reader.Read("p19222a", "phase: p1\ngoal: g\ndone:\n  - it builds\n# - it builds: met\n");

        draft!.SpecId.Should().Be("p19222a");
        draft.Done.Should().Equal("it builds");
    }

    [Fact]
    public void SpecRecordReader_SpecKeyedRecord_ReadsTheGoal() =>
        _reader.Read("2026-10-07-1891a", "spec: 2026-10-07-1891a\ngoal: g\n")!.Goal.Should().Be("g");

    [Theory]
    [InlineData("spec: p1\ngoal: [never closed\n")]
    [InlineData("goal: no id key\n")]
    [InlineData("spec: p1\n")]
    [InlineData("just a line of prose")]
    public void SpecRecordReader_UnreadableBody_IsNull(string body) =>
        _reader.Read("p1", body).Should().BeNull();
}
