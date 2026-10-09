import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DialogUploadsPanel } from "../DialogUploadsPanel";
import { DialogPane } from "../DialogPane";
import { treeOf, initiallyOpen, OPEN_ALL_UP_TO } from "../uploadTree";
import type { SpecDialogFilePreview, SpecDialogReferenceFile, SpecDialogSession } from "@/types/spec-dialog";

// 2026-10-09-86e1: "die files kann man nicht ansehen" — every file of an upload is listed in a
// tree, opened in a preview over the page, and removed on its own.
const fetchReferenceFiles = vi.fn<(dialogId: string, setId: string) => Promise<SpecDialogReferenceFile[]>>();
const fetchReferenceFilePreview = vi.fn<(dialogId: string, setId: string, path: string) => Promise<SpecDialogFilePreview>>();
const deleteReferenceFile = vi.fn<(dialogId: string, setId: string, path: string) => Promise<void>>();
vi.mock("@/lib/referenceFilesApi", () => ({
  fetchReferenceFiles: (d: string, s: string) => fetchReferenceFiles(d, s),
  fetchReferenceFilePreview: (d: string, s: string, p: string) => fetchReferenceFilePreview(d, s, p),
  fetchReferenceFileBlob: () => Promise.resolve(new Blob(["x"])),
  deleteReferenceFile: (d: string, s: string, p: string) => deleteReferenceFile(d, s, p),
  fetchBlob: () => Promise.resolve(new Blob(["x"])),
}));
vi.mock("@/lib/specDialogApi", () => ({
  deleteSpecDialogReference: vi.fn(),
  deleteSpecDialogImage: vi.fn(),
  specDialogImageUrl: (id: number) => `/api/spec-dialog/images/${id}`,
}));

const FILES: SpecDialogReferenceFile[] = [
  { path: "site/index.html", bytes: 120 },
  { path: "site/css/a.css", bytes: 30 },
  { path: "site/app/main.py", bytes: 2048 },
];

function session(): SpecDialogSession {
  return {
    sessionId: "s-1", ticket: null, scope: { name: "sample", repos: [], templates: [] }, transcript: [],
    lastActivityAt: "2026-10-09T10:00:00Z", subject: null, proposal: null, filing: null, proposalTurn: null,
    images: [],
    references: [{ setId: "a", name: "site", files: 3, bytes: 2198, at: "2026-10-09T09:00:00Z" }],
    uploadBytes: 2198, uploadCapBytes: 100 * 1024 * 1024,
  };
}

beforeEach(() => {
  fetchReferenceFiles.mockReset().mockResolvedValue(FILES);
  fetchReferenceFilePreview.mockReset().mockResolvedValue(
    { path: "site/app/main.py", kind: "text", bytes: 2048, text: "print('hi')", truncated: false });
  deleteReferenceFile.mockReset().mockResolvedValue(undefined);
});

describe("Browsing an upload", () => {
  it("UploadsPanel_ExpandSet_ListsFilesAndPreviewsText", async () => {
    render(<DialogUploadsPanel session={session()} work={null} dialogId="d-1" onRefresh={vi.fn(async () => {})} />);
    const toggle = screen.getByTestId("dialog-upload-open-a");
    expect(toggle).toHaveAttribute("aria-expanded", "false");

    fireEvent.click(toggle);

    await screen.findByTestId("dialog-upload-files-a");
    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByTestId("dialog-upload-folder-site/app/")).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByTestId("dialog-upload-file-site/index.html")).toBeInTheDocument();
    fireEvent.click(screen.getByTestId("dialog-upload-folder-site/app/"));
    expect(screen.queryByTestId("dialog-upload-file-site/app/main.py")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("dialog-upload-folder-site/app/"));

    fireEvent.click(screen.getByTitle("Open site/app/main.py"));

    expect((await screen.findByTestId("dialog-upload-preview-text")).textContent).toBe("print('hi')");
    expect(fetchReferenceFilePreview).toHaveBeenCalledWith("d-1", "a", "site/app/main.py");
    fireEvent.keyDown(screen.getByTestId("dialog-upload-preview"), { key: "Escape" });
    await waitFor(() => expect(screen.queryByTestId("dialog-upload-preview")).not.toBeInTheDocument());
  });

  it("UploadsPanel_TextOverTheBound_SaysItIsCut", async () => {
    fetchReferenceFilePreview.mockResolvedValue(
      { path: "site/index.html", kind: "text", bytes: 900 * 1024, text: "<h1>", truncated: true });
    render(<DialogUploadsPanel session={session()} work={null} dialogId="d-1" onRefresh={vi.fn(async () => {})} />);
    fireEvent.click(screen.getByTestId("dialog-upload-open-a"));

    fireEvent.click(await screen.findByTitle("Open site/index.html"));

    expect((await screen.findByTestId("dialog-upload-preview-cut")).textContent).toContain("first 200 KB of 900 KB");
  });

  it("UploadsPanel_RemoveOneFile_AsksAndDeletesThatFile", async () => {
    const onRefresh = vi.fn(async () => {});
    render(<DialogUploadsPanel session={session()} work={null} dialogId="d-1" onRefresh={onRefresh} />);
    fireEvent.click(screen.getByTestId("dialog-upload-open-a"));

    fireEvent.click(await screen.findByTestId("dialog-upload-file-remove-site/css/a.css"));
    expect((await screen.findByTestId("confirm-dialog")).textContent).toContain("Remove 'site/css/a.css' from 'site'?");
    fireEvent.click(screen.getByTestId("confirm-dialog-confirm"));

    await waitFor(() => expect(deleteReferenceFile).toHaveBeenCalledWith("d-1", "a", "site/css/a.css"));
    await waitFor(() => expect(onRefresh).toHaveBeenCalled());
  });

  it("UploadsPanel_CitedSet_OffersNoFileRemove", async () => {
    const cited = session();
    cited.references![0].cited = true;
    render(<DialogUploadsPanel session={cited} work={null} dialogId="d-1" onRefresh={vi.fn(async () => {})} />);
    fireEvent.click(screen.getByTestId("dialog-upload-open-a"));

    await screen.findByTestId("dialog-upload-file-site/index.html");
    expect(screen.queryByTestId("dialog-upload-file-remove-site/index.html")).not.toBeInTheDocument();
  });

  it("DialogPane_FocusOnAnUpload_OpensTheUploadsTabOnIt", async () => {
    render(<DialogPane session={session()} projects={[]} proposal={null} filed={null} work={null}
      focus={{ tab: "uploads", setId: "a" }} onFocus={vi.fn()} dialogId="d-1" />);

    expect(screen.getByTestId("dialog-pane")).toHaveAttribute("data-tab", "uploads");
    await screen.findByTestId("dialog-upload-files-a");
    expect(screen.getByTestId("dialog-upload-open-a")).toHaveFocus();
  });
});

describe("uploadTree", () => {
  it("TreeOf_OneRootFolder_StartsInsideIt", () => {
    const root = treeOf(FILES);

    expect(root.name).toBe("site");
    expect(root.files.map((f) => f.name)).toEqual(["index.html"]);
    expect(root.folders.map((f) => [f.path, f.count])).toEqual([["site/app/", 1], ["site/css/", 1]]);
  });

  it("InitiallyOpen_LargeSet_OpensNothing", () => {
    const many = Array.from({ length: OPEN_ALL_UP_TO + 1 }, (_, i) => ({ path: `src/f${i}.ts`, bytes: 1 }));

    expect(initiallyOpen(treeOf([...many, { path: "README.md", bytes: 1 }])).size).toBe(0);
    expect(initiallyOpen(treeOf(FILES))).toEqual(new Set(["site/app/", "site/css/"]));
  });
});
