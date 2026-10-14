import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MockVersions } from "../MockVersions";
import type { SpecDialogMock } from "@/types/spec-dialog";

// 2026-10-13-8762t: a spec's mock versions on the proposal — one screenshot per version, the ratified one
// marked, Ratify on every other until the proposal is filed, and a refusal shown in its own words.
const api = vi.hoisted(() => ({
  fetchBlob: vi.fn<(path: string) => Promise<Blob>>(async () => new Blob(["jpeg"])),
  ratifySpecMock: vi.fn<(sessionId: string, specId: string, version: number) => Promise<void>>(async () => undefined),
}));
vi.mock("@/lib/referenceFilesApi", async (actual) => ({
  ...(await actual<typeof import("@/lib/referenceFilesApi")>()),
  fetchBlob: (path: string) => api.fetchBlob(path),
}));
vi.mock("@/lib/specDialogApi", () => ({
  ratifySpecMock: (s: string, id: string, v: number) => api.ratifySpecMock(s, id, v),
}));

const mock = (version: number, hasShot = true): SpecDialogMock => ({
  specId: "spec-a", version, name: `spec-a-mock-v${version}`, at: "2026-10-13T10:00:00Z", hasShot,
});

beforeEach(() => {
  api.fetchBlob.mockClear();
  api.ratifySpecMock.mockReset();
  api.ratifySpecMock.mockResolvedValue(undefined);
  URL.createObjectURL = vi.fn(() => "blob:shot");
  URL.revokeObjectURL = vi.fn();
});

describe("MockVersions", () => {
  it("renders one thumbnail per version and marks the ratified one", async () => {
    render(<MockVersions specId="spec-a" mocks={[mock(1), mock(2)]} ratified={2} sessionId="s-1" filed={false} />);

    await waitFor(() => expect(screen.getByTestId("dialog-mock-version-shot-spec-a-v1").tagName).toBe("IMG"));
    expect(screen.getByTestId("dialog-mock-version-shot-spec-a-v2").tagName).toBe("IMG");
    expect(api.fetchBlob).toHaveBeenCalledWith("/api/spec-dialog/conversations/s-1/mocks/spec-a/1/shot");
    expect(screen.getByTestId("dialog-mock-version-spec-a-v2")).toHaveAttribute("data-ratified", "true");
    expect(screen.getByTestId("dialog-mock-version-spec-a-v2")).toHaveTextContent("ratified");
    expect(screen.queryByTestId("dialog-mock-ratify-spec-a-v2")).toBeNull();
    expect(screen.getByTestId("dialog-mock-ratify-spec-a-v1")).toBeInTheDocument();
  });

  it("Ratify posts the version and refreshes on success", async () => {
    const refresh = vi.fn(async () => undefined);
    render(<MockVersions specId="spec-a" mocks={[mock(1), mock(2)]} ratified={null} sessionId="s-1" onRatified={refresh} filed={false} />);

    fireEvent.click(screen.getByTestId("dialog-mock-ratify-spec-a-v2"));

    expect(screen.getByTestId("dialog-mock-ratify-spec-a-v1")).toBeDisabled();
    await waitFor(() => expect(refresh).toHaveBeenCalledTimes(1));
    expect(api.ratifySpecMock).toHaveBeenCalledWith("s-1", "spec-a", 2);
    expect(screen.getByTestId("dialog-mock-ratify-spec-a-v1")).not.toBeDisabled();
  });

  it("shows the server's refusal sentence on 409 and leaves the strip unchanged", async () => {
    const refresh = vi.fn(async () => undefined);
    api.ratifySpecMock.mockRejectedValue(new Error("The design partner is computing a turn; ratify when it has answered."));
    render(<MockVersions specId="spec-a" mocks={[mock(1)]} ratified={null} sessionId="s-1" onRatified={refresh} filed={false} />);

    fireEvent.click(screen.getByTestId("dialog-mock-ratify-spec-a-v1"));

    await waitFor(() => expect(screen.getByTestId("dialog-mock-refused-spec-a"))
      .toHaveTextContent("The design partner is computing a turn; ratify when it has answered."));
    expect(refresh).not.toHaveBeenCalled();
    expect(screen.getByTestId("dialog-mock-version-spec-a-v1")).not.toHaveAttribute("data-ratified");
    expect(screen.getByTestId("dialog-mock-ratify-spec-a-v1")).not.toBeDisabled();
  });

  it("shows 'no screenshot' for a version without one", () => {
    render(<MockVersions specId="spec-a" mocks={[mock(1, false)]} ratified={null} sessionId="s-1" filed={false} />);

    expect(screen.getByTestId("dialog-mock-version-noshot-spec-a-v1")).toHaveTextContent("v1 — no screenshot");
    expect(api.fetchBlob).not.toHaveBeenCalled();
  });

  it("MockVersions_AfterFiling_ShowsNoRatifyButton", () => {
    render(<MockVersions specId="spec-a" mocks={[mock(1), mock(2)]} ratified={1} sessionId="s-1" filed />);

    expect(screen.queryByTestId("dialog-mock-ratify-spec-a-v2")).toBeNull();
    expect(screen.getByTestId("dialog-mock-version-spec-a-v1")).toHaveAttribute("data-ratified", "true");
    expect(screen.getByTestId("dialog-mock-filed-spec-a"))
      .toHaveTextContent("filed with v1; a new ratification reaches the spec only through a new approval of this ticket");
  });
});
