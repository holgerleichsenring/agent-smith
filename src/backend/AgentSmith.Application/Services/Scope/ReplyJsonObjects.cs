namespace AgentSmith.Application.Services.Scope;

/// <summary>
/// p0413a: every balanced <c>{…}</c> span in a model reply, in the order it
/// appears — the tolerance both scope-reply readers need (a fenced block, a bare
/// object, or an object buried in prose). Split out of
/// <see cref="RepoScopeParser"/> when a second reader appeared: which object is
/// the wanted one is each reader's own question, finding the objects is not.
/// </summary>
internal static class ReplyJsonObjects
{
    public static IEnumerable<string> In(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '{') continue;
            var depth = 0;
            for (var j = i; j < text.Length; j++)
            {
                if (text[j] == '{') depth++;
                else if (text[j] == '}' && --depth == 0)
                {
                    yield return text[i..(j + 1)];
                    i = j;
                    break;
                }
            }
        }
    }
}
