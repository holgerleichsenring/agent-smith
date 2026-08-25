using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0521: a phase is named in a few words and stated in one sentence.
/// <para>
/// The drift was measured, not felt: mean slug length ran 31 characters at p00xx and 54
/// at p05xx, and the median goal reached 251 characters with a longest of 3631. The rule
/// already existed as guidance and was followed less every quarter. A number that fails a
/// build survives.
/// </para>
/// <para>
/// Scope is a NAMESPACE, not an ordering. The counter namespace closed, so the two id
/// shapes are alternatives and a date-minted id sorts BELOW every counter id as text — a
/// cutoff phrased "at or above this id" would exempt every phase minted from now on,
/// permanently, while its own exemption test passed.
/// </para>
/// </summary>
public sealed class PhaseNameRuleTests
{
    /// <summary>
    /// Fifty, because that is what the product already mints. A repo rule tighter than
    /// <see cref="PhaseIdFactory.Slug"/> would ship a product breaking its own rule in
    /// every customer repository.
    /// </summary>
    private const int MaxSlugChars = 50;

    /// <summary>
    /// Length alone does not separate a claim from a label: "mcp-tools-call" is short and
    /// says nothing. Four words is a crude test of a real distinction — it cannot judge
    /// whether a sentence is true, but it refuses a bare noun.
    /// </summary>
    private const int MinSlugWords = 4;

    /// <summary>
    /// One sentence. Stated HERE and not in the embedded schema, which the deployed
    /// server evaluates on every model-authored draft — see
    /// <see cref="PhaseSpecFileSchemaTests"/>.
    /// </summary>
    private const int MaxGoalChars = 200;

    [Fact]
    public void PhaseSlug_ADateMintedPhase_FitsTheLengthBound() =>
        Offenders(PhaseNameBaseline.SlugLength,
                file => file.Slug.Length > MaxSlugChars, file => file.Slug.Length)
            .Should().BeEmpty(
                $"a phase name fits a line — at most {MaxSlugChars} characters, which is "
                + "what PhaseIdFactory.Slug already mints. Do not add a baseline row.");

    /// <summary>
    /// No exemption exists for this one, and none is needed: every phase in the open
    /// namespace already states something. The floor pins a convention that holds.
    /// </summary>
    [Fact]
    public void PhaseSlug_ADateMintedPhase_UsesEnoughWordsToStateSomething() =>
        Offenders(rule: null, file => file.SlugWords < MinSlugWords, file => file.SlugWords)
            .Should().BeEmpty(
                $"a phase name states something — at least {MinSlugWords} words. A topic "
                + "label names the area and leaves the claim unwritten.");

    [Fact]
    public void PhaseGoal_ADateMintedPhase_FitsOneSentence() =>
        Offenders(PhaseNameBaseline.GoalLength,
                file => file.GoalLength > MaxGoalChars, file => file.GoalLength)
            .Should().BeEmpty(
                $"a goal is one sentence, at most {MaxGoalChars} characters. The reasoning "
                + "belongs in decisions: — do not add a baseline row.");

    /// <summary>
    /// The exemption is real, not vacuous: the closed namespace holds phases that break
    /// every one of the three bounds, and the rules above are green anyway. Asserting the
    /// violations EXIST is what stops the scoping being silently correct for the wrong
    /// reason — an ordering cutoff would also leave these green, while exempting the open
    /// namespace too.
    /// </summary>
    [Fact]
    public void PhaseSlug_ACounterNamespacePhase_IsNotJudged()
    {
        var closed = PhaseSpecFile.All().Where(file => !file.IsDateMinted).ToList();

        closed.Should().Contain(file => file.Slug.Length > MaxSlugChars,
            "the closed namespace holds names longer than the bound and keeps them");
        closed.Should().Contain(file => file.SlugWords < MinSlugWords,
            "the closed namespace holds topic labels and keeps them");
    }

    [Fact]
    public void PhaseGoal_ACounterNamespacePhase_IsNotJudged() =>
        PhaseSpecFile.All()
            .Where(file => !file.IsDateMinted && file.GoalLength > MaxGoalChars)
            .Should().NotBeEmpty(
                "the closed namespace holds goals well past the bound — 424 of them — and "
                + "none is ever rewritten, so the rule must not reach them");

    /// <summary>
    /// One number in two places. A rule tighter than the generator would refuse names the
    /// product itself mints; a rule looser would let the generator define the convention.
    /// </summary>
    [Fact]
    public void SlugGenerator_AndTheRule_AgreeOnOneNumber()
    {
        PhaseIdFactory.MaxSlugLength.Should().Be(MaxSlugChars);
        PhaseIdFactory.Slug(new string('x', 400) + " and then some")
            .Length.Should().BeLessThanOrEqualTo(MaxSlugChars,
                "whatever the generator mints has to pass the rule that judges it");
    }

    /// <summary>
    /// The bound is also stated where a phase is AUTHORED. The methodology skill states it
    /// too, in another repository on its own release cycle — deliberately not read here,
    /// the same ruling <see cref="PhaseIdSchemaTests"/> records for the id pattern.
    /// </summary>
    [Fact]
    public void Guidance_AndTheRule_StateTheSameNumber()
    {
        var guidance = File.ReadAllText(
            Path.Combine(ArchitectureSources.RepositoryRoot, "CLAUDE.md"));

        guidance.Should().Contain($"{MaxSlugChars} characters");
        guidance.Should().Contain($"{MinSlugWords} words");
        guidance.Should().Contain($"{MaxGoalChars} characters");
    }

    [Fact]
    public void PhaseName_ABaselinedPhase_DidNotGrow()
    {
        var now = Measured();
        var grown = PhaseNameBaseline.Rows
            .Where(row => now.TryGetValue(row.Key, out var value) && value > row.Value)
            .Select(row => $"{row.Key.Rule} {row.Key.PhaseId}: {row.Value} → {now[row.Key]}")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        grown.Should().BeEmpty(
            "a phase already over a bound may only get shorter.\n  " + string.Join("\n  ", grown));
    }

    [Fact]
    public void PhaseName_APhaseThatNowFits_MustLeaveTheBaseline()
    {
        var now = Measured();
        var stale = PhaseNameBaseline.Rows
            .Where(row => !now.TryGetValue(row.Key, out var value) || value <= Cap(row.Key.Rule))
            .Select(row => $"{row.Key.Rule} {row.Key.PhaseId}")
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "a phase that now fits its bound (or is gone) must leave phase-name-baseline.tsv, "
            + "so the list keeps telling the truth.\n  " + string.Join("\n  ", stale));
    }

    private static int Cap(string rule) => rule switch
    {
        PhaseNameBaseline.SlugLength => MaxSlugChars,
        PhaseNameBaseline.GoalLength => MaxGoalChars,
        _ => PhaseSpecSchemaFile.GoalMaxLength,
    };

    /// <summary>
    /// What every rule measures on every phase TODAY, so the ratchet can compare a
    /// baselined row against the current file. The word floor takes no rows and so needs
    /// no measurement here.
    /// </summary>
    private static IReadOnlyDictionary<(string Rule, string PhaseId), int> Measured() =>
        PhaseSpecFile.All()
            .SelectMany(file => new[]
            {
                ((PhaseNameBaseline.SlugLength, file.PhaseId), file.Slug.Length),
                ((PhaseNameBaseline.GoalLength, file.PhaseId), file.GoalLength),
                ((PhaseNameBaseline.SchemaGoalLength, file.PhaseId), file.GoalLength),
            })
            .GroupBy(pair => pair.Item1)
            // Some counter ids name more than one file. The ratchet judges the worst of
            // them, so a duplicate cannot hide behind its shorter twin.
            .ToDictionary(group => group.Key, group => group.Max(pair => pair.Item2));

    private static IReadOnlyList<string> Offenders(
        string? rule, Func<PhaseSpecFile, bool> breaks, Func<PhaseSpecFile, int> measure) =>
    [
        .. PhaseSpecFile.All()
            .Where(file => file.IsDateMinted && breaks(file))
            .Where(file => rule is null || !PhaseNameBaseline.Exempts(rule, file.PhaseId))
            .Select(file => $"{file.PhaseId}: {measure(file)} — {file.Slug}")
            .OrderBy(text => text, StringComparer.Ordinal),
    ];
}
