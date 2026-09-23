using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-09-23-e7f0: nothing is constructed with an argument the container could hold. A supplied
/// argument reaches a constructor parameter by its RUNTIME type and a null value has none, so a
/// registration handing over a block an installation may not declare is rejected the moment the
/// type is built — which is startup (2026-09-23-2c60). The rule is about the ARGUMENT, not its
/// type: widening the parameter to nullable satisfies a nullability rule and forwards the same
/// null, while with no argument there is nothing left to match. Activating with NO extra
/// argument stays legal.
/// </summary>
public sealed class ActivationArgumentRuleTests
{
    private const string Activator = "ActivatorUtilities";
    private const string Method = "CreateInstance";

    [Fact]
    public void Activation_NoRegistration_PassesAConstructorArgument()
    {
        SuppliedArgumentsIn($"{Activator}.{Method}<Thing>(sp, value);")
            .Should().ContainSingle("a scan that sees nothing would pass this rule forever")
            .Which.Arguments.Should().Be(1);
        SuppliedArgumentsIn($"{Activator}\n            .{Method}<Thing>(sp, value);")
            .Should().ContainSingle("a call wrapped across two lines is the same call");

        var offenders = ArchitectureSources.HandWrittenBackendFiles()
            .SelectMany(path => SuppliedArgumentsIn(File.ReadAllText(path))
                .Select(found =>
                    $"{Path.GetFileName(path)}:{found.Line} supplies {found.Arguments} argument(s)"))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "an argument reaches a parameter by its runtime type, and a null has none. Register "
            + "the value instead — a KEYED instance where the composed one must stay "
            + "distinguishable from the container's — and let the container build the type.\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// Every activation in <paramref name="source"/> that supplies a constructor argument, with
    /// the line it sits on. The provider is the first argument and the non-generic form names the
    /// type as its second, so those two are what "no argument" looks like.</summary>
    private static IEnumerable<(int Line, int Arguments)> SuppliedArgumentsIn(string source)
    {
        var at = source.IndexOf(Activator, StringComparison.Ordinal);
        while (at >= 0)
        {
            var cursor = PastMethodName(source, at + Activator.Length);
            if (cursor > 0)
            {
                var generic = cursor < source.Length && source[cursor] == '<';
                if (generic) cursor = PastMatching(source, cursor, '<', '>');
                if (cursor > 0 && cursor < source.Length && source[cursor] == '(')
                {
                    var close = PastMatching(source, cursor, '(', ')');
                    var supplied = close < 0
                        ? 0
                        : TopLevelArguments(source[(cursor + 1)..(close - 1)]) - (generic ? 1 : 2);
                    if (supplied > 0) yield return (LineOf(source, at), supplied);
                }
            }
            at = source.IndexOf(Activator, at + Activator.Length, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Just past <c>.CreateInstance</c>, or -1 where this mention of the type is something else.
    /// The whitespace is the point: a call wrapped onto the next line is the same call, and a rule
    /// seeing only the unwrapped one is defeated by a line break.</summary>
    private static int PastMethodName(string source, int afterType)
    {
        var cursor = PastWhitespace(source, afterType);
        if (cursor >= source.Length || source[cursor] != '.') return -1;
        cursor = PastWhitespace(source, cursor + 1);
        return source.AsSpan(cursor).StartsWith(Method, StringComparison.Ordinal)
            ? cursor + Method.Length
            : -1;
    }

    private static int PastWhitespace(string source, int from)
    {
        while (from < source.Length && char.IsWhiteSpace(source[from])) from++;
        return from;
    }

    private static int PastMatching(string source, int open, char opening, char closing)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == opening) depth++;
            else if (source[i] == closing && --depth == 0) return i + 1;
        }

        return -1;
    }

    // Coarse on purpose: a comma in a string literal would overcount, which changes the number in
    // the message and not the verdict. Angle brackets are not nested: the lambda arrow carries one
    // and would unbalance every count that tried.
    private static int TopLevelArguments(string arguments)
    {
        if (arguments.Trim().Length == 0) return 0;
        var depth = 0;
        var count = 1;
        foreach (var character in arguments)
        {
            if (character is '(' or '[' or '{') depth++;
            else if (character is ')' or ']' or '}') depth--;
            else if (character == ',' && depth == 0) count++;
        }

        return count;
    }

    private static int LineOf(string source, int index) =>
        source.Take(index).Count(character => character == '\n') + 1;
}
