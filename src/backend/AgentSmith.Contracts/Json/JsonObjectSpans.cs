namespace AgentSmith.Contracts.Json;

/// <summary>
/// 2026-09-07-24ed: finds JSON objects inside free-form model output. Text-shape work
/// only — what an object MEANS is each parser's business. One implementation, because
/// five brace counters existed and a brace inside a string literal defeated four of them:
/// a worker envelope whose edit carried <c>"old_string": "{\n  \"extends\": …"</c> never
/// closed, was read as narration, and the run stopped idle with the work still open.
/// </summary>
public static class JsonObjectSpans
{
    /// <summary>
    /// Every balanced <c>{...}</c> span, outermost first, so a nested payload never masks
    /// the object that contains it. A brace inside a JSON string literal is text, not
    /// structure, and a backslash escapes the character after it. Quotes are tracked
    /// only while inside an object — prose before the object may quote freely. A span
    /// that never closes is skipped and the scan resumes at the next brace, so a stray
    /// brace in prose does not hide the object behind it; a half-finished trailing object
    /// is never yielded.
    /// </summary>
    public static IEnumerable<string> Balanced(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') continue;
            var end = FindObjectEnd(text, i);
            if (end < 0) continue;
            yield return text[i..(end + 1)];
            i = end;
        }
    }

    /// <summary>
    /// 2026-10-26-cb25d: every balanced span that opens at an <paramref name="open"/> character, in the order they
    /// open — inner spans included, because what a span MEANS is the caller's call, and an outer one that does not
    /// read as an answer may hold one that does. Strings and escapes are read as <see cref="Balanced"/> reads them; a
    /// start that never closes (a truncated answer, a stray bracket) yields nothing. One linear scan per start.
    /// </summary>
    public static IEnumerable<string> Candidates(string text, char open, char close)
    {
        for (var i = text.IndexOf(open); i >= 0; i = text.IndexOf(open, i + 1))
        {
            var end = FindEnd(text, i, open, close);
            if (end >= 0) yield return text[i..(end + 1)];
        }
    }

    private static int FindObjectEnd(string text, int start) => FindEnd(text, start, '{', '}');

    private static int FindEnd(string text, int start, char open, char close)
    {
        var depth = 0;
        var inString = false;
        for (var j = start; j < text.Length; j++)
        {
            var c = text[j];
            if (inString)
            {
                if (c == '\\') j++;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == open) depth++;
            else if (c == close && --depth == 0) return j;
        }
        return -1;
    }
}
