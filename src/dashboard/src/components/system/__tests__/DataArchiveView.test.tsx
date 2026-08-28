import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ArchivePreview } from "@/lib/archiveApi";
import { DataArchiveView } from "../DataArchiveView";

// 2026-08-28-3793: the installation surface's archive half. Every claim here is one the
// operator has to be able to act on BEFORE the file leaves the server — what it carries,
// that it is unredacted, and, when a restore is refused, which rule refused it.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

const fetchMock = vi.fn();

const PREVIEW: ArchivePreview = {
  schemaHead: "WidenMoneyPrecision",
  provider: "Microsoft.EntityFrameworkCore.Sqlite",
  tables: [
    { table: "Runs", rows: 1204 },
    { table: "RunEvents", rows: 98211 },
    { table: "ConfigEntities", rows: 7 },
  ],
  totalRows: 99422,
};

function servingPreview() {
  fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => PREVIEW });
}

/** The preview answers, then the next call (a download or a restore) answers this. */
function thenAnswering(response: Partial<Response> & Record<string, unknown>) {
  fetchMock
    .mockResolvedValueOnce({ ok: true, status: 200, json: async () => PREVIEW })
    .mockResolvedValueOnce(response);
}

async function shown(): Promise<HTMLElement> {
  render(<DataArchiveView />);
  return waitFor(() => screen.getByTestId("archive-tables"));
}

describe("DataArchiveView", () => {
  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("confirm", () => true);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("Surface_BeforeTheDownload_NamesTheTablesTheirRowCountsAndWhenTheSizeIsKnown", async () => {
    servingPreview();

    const tables = await shown();

    expect(tables.textContent).toContain("Runs");
    expect(tables.textContent).toContain("98,211 rows");
    expect(screen.getByTestId("archive-view").textContent).toContain("99,422 rows");
    // The size is the one number nobody can state up front, and saying so is the honest
    // alternative to inventing it: a zip is produced as it streams.
    expect(screen.getByTestId("archive-unredacted").textContent).toContain(
      "size is not known before it is written",
    );
  });

  it("Surface_SaysTheArchiveIsUnredacted", async () => {
    servingPreview();

    await shown();

    const note = screen.getByTestId("archive-unredacted").textContent ?? "";
    expect(note).toContain("UNREDACTED");
    expect(note).toContain("secret");
  });

  it("Surface_ARefusedRestore_ShowsTheCauseNotAStatusCode", async () => {
    thenAnswering({
      ok: false,
      status: 409,
      json: async () => ({
        refusal:
          "This installation has already recorded 12 run(s). An archive is restored into "
          + "an installation that has run nothing. Nothing was written.",
      }),
    });
    await shown();

    await pickAFile();

    const refusal = await waitFor(() => screen.getByTestId("archive-refusal"));
    expect(refusal.textContent).toContain("already recorded 12 run(s)");
    expect(refusal.textContent).toContain("Nothing was written");
    expect(refusal.textContent).not.toContain("409");
  });

  it("Surface_ARestoreThatLanded_SaysWhatItWroteAndThatNoRestartIsNeeded", async () => {
    thenAnswering({
      ok: true,
      status: 200,
      json: async () => ({
        schemaHead: "WidenMoneyPrecision",
        tables: [{ table: "Runs", rows: 1204 }],
        totalRows: 99422,
      }),
    });
    await shown();

    await pickAFile();

    const restored = await waitFor(() => screen.getByTestId("archive-restored"));
    expect(restored.textContent).toContain("99,422 rows");
    expect(restored.textContent).toContain("no restart");
  });

  it("Surface_ABrowserWithNoSaveDialog_SaysTheTabHeldTheWholeFile", async () => {
    // The honest half of the download decision: where showSaveFilePicker does not exist the
    // archive IS buffered in this tab, and the surface says so rather than claiming it was
    // streamed past the browser.
    thenAnswering({
      ok: true,
      status: 200,
      body: null,
      blob: async () => ({ size: 41_943_040 }) as Blob,
    });
    vi.stubGlobal("URL", { createObjectURL: () => "blob:archive", revokeObjectURL: () => {} });
    await shown();

    fireEvent.click(screen.getByTestId("archive-download"));

    const note = await waitFor(() => screen.getByTestId("archive-download-note"));
    expect(note.textContent).toContain("40.0 MB");
    expect(note.textContent).toContain("held in this tab");
  });
});

async function pickAFile(): Promise<void> {
  const input = screen.getByTestId("archive-restore-file") as HTMLInputElement;
  const file = new File(["not really a zip"], "agentsmith-archive.zip", {
    type: "application/zip",
  });
  Object.defineProperty(input, "files", { value: [file], configurable: true });
  fireEvent.change(input);
}
