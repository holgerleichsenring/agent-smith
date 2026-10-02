using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-283de: render_reference — an uploaded website or a public URL rendered in the
/// conversation's browser sandbox. The text is exact (computed styles as the browser computed
/// them, what failed, what the egress guard refused); the screenshots go to the loop through
/// <see cref="IToolImageDeposit"/>. A URL is judged before anything spawns. Calls are serial: one
/// browser sandbox serves the conversation.
/// </summary>
public sealed class RenderReferenceToolHost(RenderReferenceServices services, RenderReferenceScope scope) : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode) =>
        [AIFunctionFactory.Create(RenderReference, name: "render_reference")];

    [Description("Renders an uploaded website (reference:<name>, optionally reference:<name>/<page>.html), an .html file in a repository by its path (a design mock beside a spec, a built page; its directory tree is copied with it) or a public http(s) URL in a real browser. Returns each selector's computed styles exactly as the browser computed them (colour, background, font, size, weight, line-height, spacing, border, radius, shadow, box size), console errors, failed requests and requests the egress guard refused, and shows desktop (1440x900) and mobile (390x844) screenshots.")]
    public async Task<string> RenderReference(
        [Description("reference:<name>[/<page>] for an uploaded website, [<repo>/]<path>.html for a page in a repository, or an absolute http(s) URL.")] string source,
        [Description("CSS selectors to report computed styles for, at most 20. Default: body, h1, h2, h3, a, button, input, nav, header, footer.")] string[]? selectors = null,
        CancellationToken ct = default)
    {
        var chosen = selectors is { Length: > 0 } ? selectors.Select(s => s.Trim()).ToList() : [.. BrowserStyleProperties.DefaultSelectors];
        if (chosen.Count > BrowserStyleProperties.MaxSelectors || chosen.Any(s => s.Length is 0 or > BrowserStyleProperties.MaxSelectorLength))
            return $"Error: name 1 to {BrowserStyleProperties.MaxSelectors} non-empty selectors of at most {BrowserStyleProperties.MaxSelectorLength} characters.";
        var (parsed, refusal) = services.Sources.Parse(source, scope);
        if (parsed is null) return $"Error: {refusal}";
        if (parsed.Url is { } url && await services.Guard.RefusalForAsync(url, ct) is { } refused)
            return $"Refused: {refused}. Only public addresses are rendered.";
        await scope.Serial.WaitAsync(ct);
        try
        {
            var (output, failure) = await services.Renderer.RenderAsync(scope, parsed, chosen, ct);
            return output is null
                ? $"render_reference failed: {failure}"
                : services.Text.Render(output.Result, Deposit(output)) + NotesOf(output);
        }
        finally
        {
            scope.Serial.Release();
        }
    }

    // 2026-10-01-283dh: a page copied out of a repository names every file that could not travel.
    private static string NotesOf(BrowserRenderOutput output) =>
        output.Notes is { Count: > 0 } notes ? "\nCopying the page:\n" + string.Concat(notes.Select(n => $"- {n}\n")) : string.Empty;

    private List<string> Deposit(BrowserRenderOutput output) =>
        [.. output.Result.Shots.Zip(output.Shots, (shot, bytes) =>
        {
            var caption = $"{shot.Viewport} screenshot of {output.Result.Url}, {shot.CapturedHeight} of {shot.PageHeight} px page height";
            var deposited = services.Images.Deposit(new ToolImage("image/jpeg", bytes, caption));
            return deposited.IsAccepted
                ? $"{caption} ({shot.Width}x{shot.Height} JPEG): shown"
                : $"{caption}: not shown — {deposited.Refusal}";
        })];
}
