import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DialogUploadsPanel } from "../DialogUploadsPanel";
import { DialogPane } from "../DialogPane";
import { statusOf } from "../DialogPaneTabs";
import { ApiResponseError } from "@/lib/apiResponse";
import type { FiledWork, SpecDialogSession } from "@/types/spec-dialog";

// 2026-10-08-e8b9h: every upload of the conversation in one tab — with what it holds against the
// cap — removed only after the page's own confirmation, and never one an approval cites.
const deleteSpecDialogReference = vi.fn<(dialogId: string, setId: string) => Promise<void>>();
const deleteSpecDialogImage = vi.fn<(dialogId: string, imageId: number) => Promise<void>>();
vi.mock("@/lib/specDialogApi", () => ({
  deleteSpecDialogReference: (dialogId: string, setId: string) => deleteSpecDialogReference(dialogId, setId),
  deleteSpecDialogImage: (dialogId: string, imageId: number) => deleteSpecDialogImage(dialogId, imageId),
  specDialogImageUrl: (imageId: number) => `/api/spec-dialog/images/${imageId}`,
}));

const MB = 1024 * 1024;

function session(overrides: Partial<SpecDialogSession> = {}): SpecDialogSession {
  return {
    sessionId: "s-1",
    ticket: null,
    scope: { name: "sample", repos: ["repo-a"], templates: [] },
    transcript: [],
    lastActivityAt: "2026-10-08T10:00:00Z",
    subject: null,
    proposal: null,
    filing: null,
    proposalTurn: null,
    images: [{ id: 7, mediaType: "image/png", at: "2026-10-08T09:30:00Z", bytes: 2 * MB }],
    references: [
      { setId: "a", name: "site", files: 3, bytes: 20 * MB, at: "2026-10-08T09:00:00Z" },
      { setId: "b", name: "docs", files: 1, bytes: 18 * MB, at: "2026-10-08T09:45:00Z", cited: true },
    ],
    uploadBytes: 40 * MB,
    uploadCapBytes: 100 * MB,
    ...overrides,
  };
}

const approvedWork = { dialogId: "d-1", tickets: [], approved: { key: "k" } } as unknown as FiledWork;

beforeEach(() => {
  deleteSpecDialogReference.mockReset().mockResolvedValue(undefined);
  deleteSpecDialogImage.mockReset().mockResolvedValue(undefined);
});

function renderPanel(held = session(), work: FiledWork | null = null) {
  const onRefresh = vi.fn(async () => {});
  render(<DialogUploadsPanel session={held} work={work} dialogId="d-1" onRefresh={onRefresh} />);
  return onRefresh;
}

describe("DialogUploadsPanel", () => {
  it("DialogUploadsPanel_SetsAndImages_ListedWithUsedAndLeft", () => {
    renderPanel();

    expect(screen.getByTestId("dialog-uploads-usage").textContent).toBe("40.0 MB of 100.0 MB used, 60.0 MB left");
    const rows = screen.getAllByRole("listitem").map((row) => row.getAttribute("data-testid"));
    expect(rows).toEqual(["dialog-upload-a", "dialog-upload-7", "dialog-upload-b"]);
  });

  it("DialogUploadsPanel_Remove_AsksThenDeletesAndReloads", async () => {
    const onRefresh = renderPanel();

    fireEvent.click(screen.getByTestId("dialog-upload-remove-a"));
    const asked = await screen.findByTestId("confirm-dialog");
    expect(asked.textContent).toContain("Remove 'site'?");
    expect(asked.textContent).toContain("The design partner no longer sees it from the next turn.");
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));

    await waitFor(() => expect(deleteSpecDialogReference).toHaveBeenCalledWith("d-1", "a"));
    await waitFor(() => expect(onRefresh).toHaveBeenCalled());
  });

  it("DialogUploadsPanel_RemoveImage_PromptNamesTheMoment", async () => {
    renderPanel();

    fireEvent.click(screen.getByTestId("dialog-upload-remove-7"));

    expect((await screen.findByTestId("confirm-dialog")).textContent)
      .toContain("Remove the image attached 2026-10-08 09:30 UTC?");
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));
    await waitFor(() => expect(deleteSpecDialogImage).toHaveBeenCalledWith("d-1", 7));
  });

  it("DialogUploadsPanel_RemoveKept_SendsNothing", async () => {
    const onRefresh = renderPanel();

    fireEvent.click(screen.getByTestId("dialog-upload-remove-a"));
    fireEvent.click(await screen.findByTestId("confirm-dialog-cancel"));

    await waitFor(() => expect(screen.queryByTestId("confirm-dialog")).not.toBeInTheDocument());
    expect(deleteSpecDialogReference).not.toHaveBeenCalled();
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it("DialogUploadsPanel_CitedUpload_HasNoRemoveAndSaysCited", () => {
    renderPanel();

    expect(screen.queryByTestId("dialog-upload-remove-b")).not.toBeInTheDocument();
    expect(screen.getByTestId("dialog-upload-cited-b").textContent).toBe("cited by an approval");
  });

  it("DialogUploadsPanel_Refused409_ShowsReasonInPanel", async () => {
    const reason = "A turn is running or waiting on your approval in this conversation. Remove the upload once it is over.";
    deleteSpecDialogReference.mockRejectedValue(
      new ApiResponseError("/api/spec-dialog/references/a", 409, `HTTP 409 — ${reason}`, reason));
    const onRefresh = renderPanel();

    fireEvent.click(screen.getByTestId("dialog-upload-remove-a"));
    fireEvent.click(await screen.findByTestId("confirm-dialog-confirm"));

    expect((await screen.findByTestId("dialog-uploads-note")).textContent).toBe(reason);
    expect(onRefresh).not.toHaveBeenCalled();
  });

  it("DialogUploadsPanel_ApprovedTicket_UncitedSetSaysNextApproval", () => {
    renderPanel(session(), approvedWork);

    expect(screen.getByTestId("dialog-upload-later-a").textContent).toContain("after the ticket is approved again");
    expect(screen.queryByTestId("dialog-upload-later-b")).not.toBeInTheDocument();
  });
});

describe("DialogPane uploads tab", () => {
  it("DialogPane_UploadsTab_LabelledWithNoStatus", () => {
    render(<DialogPane session={session()} projects={[]} proposal={null} filed={null} work={null}
      focus={{ tab: "uploads" }} onFocus={() => {}} dialogId="d-1" onRefresh={async () => {}} />);

    expect(screen.getByRole("tab", { name: /Uploads/ })).toHaveAttribute("aria-selected", "true");
    expect(statusOf("uploads", null, null, null)).toBe("");
    expect(screen.getByTestId("dialog-uploads")).toBeInTheDocument();
  });

  it("DialogPane_NoUploads_NotOffered", () => {
    render(<DialogPane session={session({ images: [], references: [] })} projects={[]} proposal={null}
      filed={null} work={null} focus={null} onFocus={() => {}} />);

    expect(screen.queryByRole("tab", { name: /Uploads/ })).not.toBeInTheDocument();
  });
});
