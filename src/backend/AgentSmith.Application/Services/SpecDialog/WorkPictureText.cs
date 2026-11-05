using System.Text;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>2026-11-05-862ea: a work-picture value as the model writes it — lower case, words dashed (one-spec).</summary>
public static class WorkPictureText
{
    public static string Dashed(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var text = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsUpper(c) && text.Length > 0) text.Append('-');
            text.Append(char.ToLowerInvariant(c));
        }
        return text.ToString();
    }
}
