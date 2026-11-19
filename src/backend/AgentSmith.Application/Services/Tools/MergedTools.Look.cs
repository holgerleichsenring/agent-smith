using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>2026-11-18-9c1cd: web_fetch (design_read) and render_reference (compare_reference).</summary>
public static partial class MergedTools
{
    /// <summary>web_fetch with design_read folded in: a Figma link is read through the project's Figma source.</summary>
    public static AIFunction WebFetch(WebToolHost? web, DesignReadToolHost? design) =>
        AIFunctionFactory.Create(
            async ([Description("Absolute http(s) URL. A Figma link (https://www.figma.com/design/<key>/…?node-id=1-2) is read as a design.")] string url,
                   [Description("Maximum characters to return (default 100000).")] int? max_length = null,
                   [Description("Character offset for the next page. Default 0.")] int? start_index = null,
                   [Description("Return the raw body without HTML-to-markdown conversion.")] bool? raw = null,
                   [Description("Figma only: the design source to read through, when the project has several.")] string? source = null,
                   [Description("Figma only: levels below the node to read, 1-6 (default 3).")] int? depth = null,
                   [Description("Figma only: the file version the ticket cites; the answer says whether the design moved since.")] string? expected_version = null,
                   [Description("Figma only: true reads expected_version itself.")] bool? read_version = null,
                   CancellationToken ct = default) =>
            {
                if (IsFigma(url))
                    return design is null
                        ? "Error: this project has no Figma design source, so a Figma link cannot be read; ask for an export or a screenshot."
                        : await design.DesignRead(url, source, depth, expected_version, read_version, ct);
                return web is null
                    ? "Error: this master has no web access."
                    : await web.WebFetch(url, max_length, start_index, raw, ct);
            },
            "web_fetch",
            "Fetches a public http(s) URL as markdown (or raw) — docs, changelogs, advisories — paged with max_length and "
            + "start_index. A Figma link carrying node-id is read through the project's Figma source instead: per node its "
            + "type, size, layout, colours, text, components and variables, and a PNG render shown after the result; a "
            + "failed read names its kind (not_found, forbidden, rate_limited with its wait, unreachable).");

    private static bool IsFigma(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && (u.Host.Equals("figma.com", StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith(".figma.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>render_reference with compare_reference folded in as <c>reference</c> + <c>pairs</c>.</summary>
    public static AIFunction RenderReference(AIFunction render, AIFunction? compare) =>
        AIFunctionFactory.Create(
            async ([Description("The page: reference:<name>[/<page>] for an upload, [<repo>/]<path>.html in a repository, or an absolute http(s) URL.")] string source,
                   [Description("CSS selectors to report computed styles for, at most 20 (render only).")] string[]? selectors = null,
                   [Description("Compare: the reference page, in the same forms; source is then the candidate.")] string? reference = null,
                   [Description("Compare: selector pairs, at most 30, 'reference selector => candidate selector' or one selector for both.")] string[]? pairs = null,
                   [Description("Compare: desktop (default), mobile or both.")] string? viewport = null,
                   CancellationToken ct = default) =>
                string.IsNullOrWhiteSpace(reference)
                    ? await Call(render, string.Empty, ct, ("source", source), ("selectors", selectors))
                    : await Call(compare, "Error: comparing is not available here.", ct,
                        ("reference", reference), ("candidate", source), ("pairs", pairs ?? []), ("viewport", viewport)),
            "render_reference",
            "Renders a page in a real browser: an upload (reference:<name>[/<page>]), an .html file in a repository, or a "
            + "public URL. Returns each selector's computed styles exactly as the browser computed them, console errors, "
            + "failed and refused requests, and shows desktop (1440x900) and mobile (390x844) screenshots. With reference "
            + "and pairs it compares source (the candidate) against reference instead: every computed-style property that "
            + "differs per selector pair, with both values, and the share of differing pixels with a diff image. It reports; it decides nothing.");
}
