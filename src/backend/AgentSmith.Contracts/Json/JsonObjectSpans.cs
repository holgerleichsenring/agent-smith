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

    private static int FindObjectEnd(string text, int start)
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
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return j;
        }
        return -1;
    }
}
