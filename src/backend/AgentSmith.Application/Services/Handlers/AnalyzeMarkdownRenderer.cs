using System.Text;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0243, extracted by 2026-09-04-0721: "what the agent understood before it started", as the
/// operator reads it on the dashboard after the Analyze step. Rendering that story changes for
/// reasons that have nothing to do with running an analyzer, and the handler had reached the
/// length its ratchet entry allows.
/// </summary>
internal static class AnalyzeMarkdownRenderer
{
    // p0243: render the per-repo ProjectMap(s) as operator-readable markdown —
    // language, build/test commands, modules, test projects, conventions. This is
    // "what the agent understood before it started"; the dashboard shows it after
    // the Analyze step so the operator isn't flying blind on the agent's intent.
    public static string Render(IReadOnlyDictionary<string, ProjectMap> maps)
    {
        var sb = new StringBuilder();
        sb.Append("# Analyze — what the agent understood\n\n");
        sb.Append($"{maps.Count} context(s) analyzed.\n");
        foreach (var (key, m) in maps)
        {
            sb.Append($"\n## {key}\n\n");
            sb.Append($"- **Language:** {m.PrimaryLanguage}\n");
            if (m.Frameworks.Count > 0)
                sb.Append($"- **Frameworks:** {string.Join(", ", m.Frameworks)}\n");
            sb.Append($"- **Build:** {Code(m.Ci.BuildCommand)}\n");
            sb.Append($"- **Test:** {Code(m.Ci.TestCommand)}\n");
            sb.Append($"- **Prerequisites:** {Code(m.Prerequisites)}\n");
            if (m.EntryPoints.Count > 0)
                sb.Append($"- **Entry points:** {string.Join(", ", m.EntryPoints.Select(e => $"`{e}`"))}\n");

            sb.Append($"\n**Modules ({m.Modules.Count})**\n\n");
            foreach (var mod in m.Modules)
                sb.Append($"- `{mod.Path}` — {mod.Role}\n");

            sb.Append($"\n**Test projects ({m.TestProjects.Count})**\n\n");
            if (m.TestProjects.Count == 0)
                sb.Append("- _none discovered_\n");
            foreach (var t in m.TestProjects)
                sb.Append($"- `{t.Path}` — {t.Framework} ({t.FileCount} file(s))\n");

            if (m.Conventions is { } c &&
                (c.NamingPattern is not null || c.TestLayout is not null || c.ErrorHandling is not null))
            {
                sb.Append("\n**Conventions**\n\n");
                if (c.NamingPattern is not null) sb.Append($"- naming: {c.NamingPattern}\n");
                if (c.TestLayout is not null) sb.Append($"- test layout: {c.TestLayout}\n");
                if (c.ErrorHandling is not null) sb.Append($"- error handling: {c.ErrorHandling}\n");
            }
        }
        return sb.ToString();
    }

    private static string Code(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "_n/a_" : $"`{value}`";
}
