using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Services.Dialogue;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Dialogue;

public sealed class InMemoryDialogueTrailTests
{
    private readonly InMemoryDialogueTrail _sut = new();

    [Fact]
    public void GetAll_Empty_ReturnsEmptyList()
    {
        var result = _sut.GetAll();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_SingleEntry_CanBeRetrieved()
    {
        var question = CreateQuestion("q1", QuestionType.Confirmation, "Proceed?");
        var answer = CreateAnswer("q1", "yes");

        await _sut.RecordAsync(question, answer);

        var entries = _sut.GetAll();
        entries.Should().HaveCount(1);
        entries[0].Question.QuestionId.Should().Be("q1");
        entries[0].Answer.Answer.Should().Be("yes");
    }

    [Fact]
    public async Task RecordAsync_MultipleEntries_PreservesOrder()
    {
        var q1 = CreateQuestion("q1", QuestionType.Choice, "Pick one");
        var a1 = CreateAnswer("q1", "option-a");
        var q2 = CreateQuestion("q2", QuestionType.FreeText, "Explain");
        var a2 = CreateAnswer("q2", "some explanation");

        await _sut.RecordAsync(q1, a1);
        await _sut.RecordAsync(q2, a2);

        var entries = _sut.GetAll();
        entries.Should().HaveCount(2);
        entries[0].Question.QuestionId.Should().Be("q1");
        entries[1].Question.QuestionId.Should().Be("q2");
    }

    [Fact]
    public async Task GetAll_ReturnsSnapshot_NotLiveReference()
    {
        var question = CreateQuestion("q1", QuestionType.Info, "Note");
        var answer = CreateAnswer("q1", "ok");

        await _sut.RecordAsync(question, answer);
        var snapshot = _sut.GetAll();

        // Add another entry after snapshot
        await _sut.RecordAsync(
            CreateQuestion("q2", QuestionType.Approval, "Approve?"),
            CreateAnswer("q2", "approved"));

        snapshot.Should().HaveCount(1, "snapshot should not reflect later additions");
        _sut.GetAll().Should().HaveCount(2);
    }

    [Fact]
    public async Task RecordAsync_ThreadSafety_NoConcurrencyIssues()
    {
        var tasks = Enumerable.Range(0, 50).Select(i =>
        {
            var q = CreateQuestion($"q{i}", QuestionType.FreeText, $"Question {i}");
            var a = CreateAnswer($"q{i}", $"Answer {i}");
            return _sut.RecordAsync(q, a);
        });

        await Task.WhenAll(tasks);

        _sut.GetAll().Should().HaveCount(50);
    }

    private static DialogQuestion CreateQuestion(string id, QuestionType type, string text) =>
        new(id, type, text, null, null, null, TimeSpan.FromMinutes(5));

    private static DialogAnswer CreateAnswer(string id, string answer) =>
        new(id, answer, null, DateTimeOffset.UtcNow, "test-user");
}
