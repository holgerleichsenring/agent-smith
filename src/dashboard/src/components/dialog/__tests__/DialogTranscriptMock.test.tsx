import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DialogTranscript } from "../DialogTranscript";
import { withImages, withMocks } from "../transcriptImages";
import type { DialogEntry, TranscriptMock } from "@/hooks/useSpecDialog";

// 2026-10-13-8762r: a mock version the design partner wrote is an agent-side entry in the transcript —
// labelled with its spec and version, its desktop screenshot drawn over the shot route, and its whole
// page saved as a file, never opened from this origin.
const api = vi.hoisted(() => ({
  fetchBlob: vi.fn<(path: string) => Promise<Blob>>(async () => new Blob(["jpeg"])),
  fetchMockHtmlBlob: vi.fn<(sessionId: string, specId: string, version: number) => Promise<Blob>>(async () => new Blob(["<html>"])),
}));
vi.mock("@/lib/referenceFilesApi", async (actual) => ({
  ...(await actual<typeof import("@/lib/referenceFilesApi")>()),
  fetchBlob: (path: string) => api.fetchBlob(path),
  fetchMockHtmlBlob: (s: string, id: string, v: number) => api.fetchMockHtmlBlob(s, id, v),
}));

const said: DialogEntry = { key: "u-1", kind: "user", text: "show me the page", at: "2026-10-13T10:00:00Z" };
const replied: DialogEntry = { key: "a-1", kind: "agent", text: "here it is", at: "2026-10-13T10:05:00Z" };
const mock = (over: Partial<TranscriptMock> = {}): TranscriptMock => ({
  sessionId: "s-1", specId: "spec-a", version: 1, name: "spec-a-mock-v1", at: "2026-10-13T10:02:00Z", hasShot: true, ...over,
});

beforeEach(() => {
  api.fetchBlob.mockClear();
  api.fetchMockHtmlBlob.mockClear();
  URL.createObjectURL = vi.fn(() => "blob:mock");
  URL.revokeObjectURL = vi.fn();
});

describe("a mock in the transcript", () => {
  it("withImages_PlacesMocksByTime", () => {
    const placed = withImages([said, replied], [], [], [mock(), mock({ version: 2, name: "spec-a-mock-v2", at: "2026-10-13T10:06:00Z" })]);

    expect(placed.map((e) => e.key)).toEqual(["u-1", "mock-spec-a-v1", "a-1", "mock-spec-a-v2"]);
    expect(withMocks(placed, [mock(), mock({ version: 2, at: "2026-10-13T10:06:00Z" })])).toBe(placed);
    const refreshed = withMocks(withImages([said], [], [], [mock({ hasShot: false })]), [mock()]);
    expect(refreshed.find((e) => e.kind === "mock")?.mock?.hasShot).toBe(true);
  });

  it("DialogTranscript_MockEntry_ShowsScreenshotWithVersion", async () => {
    render(<DialogTranscript entries={withImages([said], [], [], [mock(), mock({ specId: "spec-b", name: "spec-b-mock-v1", hasShot: false })])} onInspect={() => {}} />);

    expect(screen.getByTestId("dialog-mock-spec-a-v1")).toHaveTextContent("Mock v1 — spec-a");
    await waitFor(() => expect(screen.getByTestId("dialog-mock-shot-spec-a-v1").tagName).toBe("IMG"));
    expect(api.fetchBlob).toHaveBeenCalledWith("/api/spec-dialog/conversations/s-1/mocks/spec-a/1/shot");
    expect(screen.getByTestId("dialog-mock-unrendered-spec-b-v1")).toHaveTextContent("not rendered");
  });

  it("DialogTranscript_MockEntry_OpenHtml_DownloadsThePage", async () => {
    const clicked: string[] = [];
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) {
      clicked.push(this.download);
    });
    render(<DialogTranscript entries={withImages([said], [], [], [mock({ version: 3, name: "spec-a-mock-v3" })])} onInspect={() => {}} />);

    fireEvent.click(screen.getByTestId("dialog-mock-open-spec-a-v3"));

    await waitFor(() => expect(clicked).toEqual(["spec-a-mock-v3.html"]));
    expect(api.fetchMockHtmlBlob).toHaveBeenCalledWith("s-1", "spec-a", 3);
    click.mockRestore();
  });
});
