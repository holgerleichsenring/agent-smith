using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-13-8762r: write_mock and read_mock — the design partner authors a spec's HTML mock as
/// numbered versions. Each write is stored, rendered through render_reference's machinery in the
/// conversation's browser and shown to the loop; its desktop screenshot is kept for the operator.
/// The transcript keeps text only, so read_mock is how a later turn revises the page it wrote.
/// </summary>
public sealed partial class MockWriteToolHost(
    RenderReferenceServices services, RenderReferenceScope scope,
    IConversationMocks mocks, IConversationMockReader reader) : IToolHost
{
    private const string Desktop = "desktop";

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? stage, string? investigatorMode) =>
        [AIFunctionFactory.Create(WriteMock, name: "write_mock"), AIFunctionFactory.Create(ReadMock, name: "read_mock")];

    [Description("Stores a whole HTML page as the next version of a spec's mock, renders it in the browser and shows desktop (1440x900) and mobile (390x844) screenshots; the operator sees the desktop one in the conversation. Write the whole page every time — CSS and script inline — never a fragment or a diff.")]
    public async Task<string> WriteMock(
        [Description("The spec the mock belongs to — the id the proposal names for it.")] string spec_id,
        [Description("The whole page, at most 90000 characters.")] string html,
        CancellationToken ct = default)
    {
        if (Refusal(spec_id, html) is { } refused) return $"Error: write_mock refused, nothing was stored: {refused}.";
        await scope.Serial.WaitAsync(ct);
        try
        {
            var (version, capped) = await mocks.AddAsync(scope.Conversation!, spec_id, html, ct);
            if (version is null) return $"Error: write_mock refused, nothing was stored: {capped}.";
            var head = $"Stored {spec_id} mock v{version.Version} ({version.Name}.html).";
            var (output, failure) = await RenderAsync(version, html, ct);
            if (output is null) return $"{head} The version is kept without a screenshot; the render failed: {failure}";
            if (DesktopShot(output) is { } shot) await mocks.SetShotAsync(scope.Conversation!, version.SetId, shot, ct);
            return $"{head}\n{RenderReferenceToolHost.Answer(services, output)}";
        }
        finally
        {
            scope.Serial.Release();
        }
    }

    [Description("Without spec_id: lists every mock version written in this conversation. With spec_id: returns that version's whole HTML (the newest when version is omitted) — read it before writing the next version.")]
    public async Task<string> ReadMock(
        [Description("The spec whose mock to read; omit to list every version.")] string? spec_id = null,
        [Description("The version number; omit for the newest.")] int? version = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(spec_id)) return Listing(await reader.ListAsync(scope.Conversation!, ct));
        var page = await reader.HtmlAsync(scope.Conversation!, spec_id, version, ct);
        return page is null
            ? $"Error: this conversation holds no mock {(version is { } n ? $"v{n} " : string.Empty)}of {spec_id}."
            : $"{spec_id} mock v{page.Version.Version} ({page.Version.Name}.html):\n{page.Html}";
    }

    private static string? Refusal(string specId, string html)
    {
        if (!SpecIdShape().IsMatch(specId ?? string.Empty))
            return $"'{specId}' is not a spec id (a letter or digit, then letters, digits or '-', at most 64)";
        if (string.IsNullOrWhiteSpace(html)) return "the page is empty";
        return html.Length > IConversationMocks.MaxChars
            ? $"the mock is {html.Length} characters, over the cap of {IConversationMocks.MaxChars} characters per mock"
            : null;
    }

    private async Task<(BrowserRenderOutput? Output, string? Failure)> RenderAsync(MockVersion version, string html, CancellationToken ct)
    {
        var page = $"{version.SpecId}.html";
        var source = RenderSource.OfInline(version.SetId, page, [new ReferenceSetFile(page, Encoding.UTF8.GetBytes(html))]);
        try
        {
            return await services.Renderer.RenderAsync(scope, source, [.. BrowserStyleProperties.DefaultSelectors], ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, ex.Message);
        }
    }

    // By viewport, never by position: the report names each shot's viewport.
    private static byte[]? DesktopShot(BrowserRenderOutput output) =>
        output.Result.Shots.Select((shot, i) => (shot, i))
            .Where(s => s.shot.Viewport == Desktop && s.i < output.Shots.Count)
            .Select(s => output.Shots[s.i]).FirstOrDefault();

    private static string Listing(IReadOnlyList<MockVersion> versions) =>
        versions.Count == 0
            ? "No mock has been written in this conversation yet."
            : "Mock versions, oldest first:\n" + string.Concat(versions.Select(v =>
                $"- {v.SpecId} v{v.Version} ({v.Name}.html), {v.At:u}{(v.HasShot ? string.Empty : ", not rendered")}\n"));

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9-]{0,63}$")]
    private static partial Regex SpecIdShape();
}
