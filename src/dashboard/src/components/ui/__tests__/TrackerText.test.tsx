import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { TrackerText } from "../TrackerText";

// A tracker text renders by the format its source named: formatted and sanitized, never as source.

afterEach(cleanup);

function body() {
  return screen.getByTestId("tracker-text");
}

describe("TrackerText", () => {
  it.each(["markdown", "html"] as const)("TrackerText_MentionAnchor_ReadsAtNameAndIsNoLink (%s)", (format) => {
    render(<TrackerText format={format} text={'Ask <a href="#" data-vss-mention="version:2.0,abc">@Ada Example</a> first.'} />);

    expect(body().textContent).toBe("Ask @Ada Example first.");
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("TrackerText_RealLink_StaysALink", () => {
    render(<TrackerText format="markdown" text={"See [the PR](https://example.com/pr/1)."} />);

    expect(screen.getByRole("link", { name: "the PR" }).getAttribute("href")).toBe("https://example.com/pr/1");
  });

  it("TrackerText_HtmlBody_RendersHeadingListAndLinkWithoutTagSource", () => {
    render(<TrackerText format="html" text={'<h2 id="done">Agent Smith - Completed</h2><ul><li><a href="https://example.com/pr/1">PR 1</a></li></ul>'} />);

    expect(screen.getByRole("heading", { level: 2 }).textContent).toBe("Agent Smith - Completed");
    expect(screen.getByRole("list")).toBeTruthy();
    expect(screen.getByRole("link", { name: "PR 1" }).getAttribute("href")).toBe("https://example.com/pr/1");
    expect(body().textContent).not.toContain("<");
  });

  it("TrackerText_MarkdownWithInlineHtml_RendersBoldAndALineBreak", () => {
    render(<TrackerText format="markdown" text={"<b>Agent Smith — Failed</b><br/><b>Error:</b> boom"} />);

    expect(body().querySelectorAll("b, strong")).toHaveLength(2);
    expect(body().querySelector("br")).not.toBeNull();
    expect(body().textContent).not.toContain("<b>");
  });

  it.each([
    ["html", '<p>a</p><script>alert(1)</script><iframe src="https://evil.example"></iframe><p onclick="alert(1)">b</p><a href="javascript:alert(1)">c</a>'],
    ["markdown", 'a\n\n<script>alert(1)</script>\n\n<iframe src="https://evil.example"></iframe>\n\n<p onclick="alert(1)">b</p>\n\n[c](javascript:alert(1))'],
  ] as const)("TrackerText_UnsafeContent_IsRemoved (%s)", (format, text) => {
    render(<TrackerText format={format} text={text} />);

    const html = body().innerHTML;
    expect(body().querySelector("script, iframe")).toBeNull();
    expect(html).not.toContain("onclick");
    expect(html).not.toContain("javascript:");
    expect(body().textContent).toContain("b");
  });

  it("TrackerText_HtmlImage_RendersAsALink", () => {
    render(<TrackerText format="html" text={'<p><img src="https://tracker.example/_apis/attachments/1" alt="screenshot"></p>'} />);

    expect(body().querySelector("img")).toBeNull();
    expect(screen.getByRole("link", { name: "screenshot" }).getAttribute("href")).toBe("https://tracker.example/_apis/attachments/1");
  });

  it("TrackerText_TextBody_KeepsItsLineBreaks", () => {
    render(<TrackerText format="text" text={"first line\nsecond line"} />);

    expect(body().textContent).toBe("first line\nsecond line");
    expect(body().style.whiteSpace).toBe("pre-wrap");
  });

  it("TrackerText_EscapedAngleBracketFromJira_ShowsTheText", () => {
    render(<TrackerText format="markdown" text={"Use List\\<string> here"} />);

    expect(body().textContent).toBe("Use List<string> here");
  });

  it("TrackerText_MarkdownTaskList_RendersDisabledCheckboxes", () => {
    render(<TrackerText format="markdown" text={"- [x] done\n- [ ] open"} />);

    const boxes = body().querySelectorAll("input[type=checkbox]");
    expect(boxes).toHaveLength(2);
    expect((boxes[0] as HTMLInputElement).checked).toBe(true);
    expect((boxes[1] as HTMLInputElement).disabled).toBe(true);
  });

  it("TrackerText_HtmlChecklistAsMarkdigWritesIt_RendersCheckedDisabledBoxes", () => {
    render(<TrackerText format="html" text={'<ul class="contains-task-list"><li class="task-list-item"><input disabled="disabled" type="checkbox" checked="checked" /> merged</li><li class="task-list-item"><input disabled="disabled" type="checkbox" /> deployed</li></ul>'} />);

    const boxes = body().querySelectorAll("input[type=checkbox]");
    expect(boxes).toHaveLength(2);
    expect((boxes[0] as HTMLInputElement).checked).toBe(true);
    expect((boxes[1] as HTMLInputElement).checked).toBe(false);
    expect(body().textContent).toContain("merged");
  });
});
