import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "@/lib/apiResponse";
import type { PocTarget, PocUpload, PocUploadMatch } from "@/lib/pocsApi";
import { PocImportFlow } from "../PocImport";

// 2026-11-23-933db: the import page — the checks from the upload answer, the proposed name that can be edited, the
// mismatch alert for verdict ask with its three ways on, and Start posting the choice that was made.

const upload = vi.fn();
const importUpload = vi.fn();

vi.mock("@/lib/pocsApi", () => ({
  uploadPocZip: (...args: unknown[]) => upload(...args),
  importPocUpload: (...args: unknown[]) => importUpload(...args),
}));

const ONE: PocTarget[] = [{ name: "main", sourceRepo: "github/poc-source", root: "pocs" }];
const TWO: PocTarget[] = [...ONE, { name: "lab", sourceRepo: "github/poc-lab", root: "pocs" }];

function match(over: Partial<PocUploadMatch> = {}): PocUploadMatch {
  return {
    pocId: 7, pocName: "shift-planner", revision: 12, added: 3, changed: 10, removed: 1, unchanged: 200,
    ratioPercent: 6, portChanged: false, entrypointChanged: false, previousPort: 8000, verdict: "same", ...over,
  };
}

function answer(over: Partial<PocUpload> = {}): PocUpload {
  return {
    uploadId: "u-1", fileName: "shift-planner-v4.zip", target: "main", port: 8000, entryCount: 214,
    unpackedBytes: 6 * 1024 * 1024, proposedName: "shift-planner-v4",
    checks: ["paths", "kinds", "bounds", "forbidden", "dockerfile", "expose"], match: null, ...over,
  };
}

async function drop(targets = ONE) {
  render(<PocImportFlow targets={targets} />);
  fireEvent.change(screen.getByTestId("poc-import-input"), { target: { files: [new File(["zip"], "shift-planner-v4.zip")] } });
  await screen.findByTestId("poc-import-checks");
}

describe("PocImportFlow", () => {
  beforeEach(() => {
    upload.mockReset();
    importUpload.mockReset();
  });

  it("PocImport_Upload_RendersTheChecksFromTheAnswer", async () => {
    upload.mockResolvedValue(answer({ checks: ["paths", "bounds", "expose"] }));
    await drop();

    expect(upload).toHaveBeenCalledWith(expect.any(File), undefined);
    expect(screen.getByTestId("poc-import-file")).toHaveTextContent("shift-planner-v4.zip");
    expect(screen.getByTestId("poc-import-check-paths")).toHaveTextContent("Safe paths");
    expect(screen.getByTestId("poc-import-check-bounds")).toHaveTextContent("214 files, 6.0 MB unpacked");
    expect(screen.getByTestId("poc-import-check-expose")).toHaveTextContent("EXPOSE 8000");
    expect(screen.queryByTestId("poc-import-check-kinds")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-import-next")).toHaveTextContent("nothing is committed");
  });

  it("PocImport_NameField_ShowsTheProposal_AndCanBeEdited", async () => {
    upload.mockResolvedValue(answer());
    await drop();

    expect(screen.queryByTestId("poc-import-app")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-import-name")).toHaveValue("shift-planner-v4");
    fireEvent.change(screen.getByTestId("poc-import-name"), { target: { value: "rota" } });
    expect(screen.getByTestId("poc-import-name")).toHaveValue("rota");
  });

  it("PocImport_StartNew_PostsTheNameAndTarget_ThenLinksTheRun", async () => {
    upload.mockResolvedValue(answer());
    importUpload.mockResolvedValue({ pocId: 9, revision: 1, runId: "r-42" });
    await drop();
    fireEvent.change(screen.getByTestId("poc-import-name"), { target: { value: "rota" } });

    fireEvent.click(screen.getByTestId("poc-import-start"));

    await screen.findByTestId("poc-import-started");
    expect(importUpload).toHaveBeenCalledWith("u-1", { choice: "new", target: "main", name: "rota" });
    expect(screen.getByTestId("poc-import-run")).toHaveAttribute("href", "/jobs/r-42");
    expect(screen.getByTestId("poc-import-list")).toHaveAttribute("href", "/pocs");
  });

  it("PocImport_StartReplace_PostsThePocId", async () => {
    upload.mockResolvedValue(answer({ match: match() }));
    importUpload.mockResolvedValue({ pocId: 7, revision: 13, runId: "r-43" });
    await drop();

    expect(screen.getByTestId("poc-import-app-replace")).toBeChecked();
    expect(screen.getByTestId("poc-import-compared")).toHaveTextContent("Compared with r12");
    expect(screen.queryByTestId("poc-import-mismatch")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("poc-import-start"));

    await screen.findByTestId("poc-import-started");
    expect(importUpload).toHaveBeenCalledWith("u-1", { choice: "replace", pocId: 7 });
  });

  it("PocImport_VerdictAsk_ShowsTheAlertWithThreeChoices_AndHoldsStart", async () => {
    upload.mockResolvedValue(answer({ match: match({ verdict: "ask", changed: 180, unchanged: 30, portChanged: true, previousPort: 3000 }) }));
    await drop();

    const alert = screen.getByTestId("poc-import-mismatch");
    expect(alert).toHaveTextContent("This doesn't look like shift-planner");
    expect(alert).toHaveTextContent("184 of 214 files differ");
    expect(alert).toHaveTextContent("The port changes from 3000 to 8000.");
    expect(screen.getByTestId("poc-import-mismatch-replace")).toHaveTextContent("Replace shift-planner anyway");
    expect(screen.getByTestId("poc-import-mismatch-new")).toBeInTheDocument();
    expect(screen.getByTestId("poc-import-mismatch-another")).toBeInTheDocument();
    expect(screen.getByTestId("poc-import-start")).toBeDisabled();
  });

  it("PocImport_ReplaceAfterTheAlert_PostsConfirmed", async () => {
    upload.mockResolvedValue(answer({ match: match({ verdict: "ask" }) }));
    importUpload.mockResolvedValue({ pocId: 7, revision: 13, runId: "r-44" });
    await drop();

    fireEvent.click(screen.getByTestId("poc-import-mismatch-replace"));
    expect(screen.queryByTestId("poc-import-mismatch")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("poc-import-start"));

    await screen.findByTestId("poc-import-started");
    expect(importUpload).toHaveBeenCalledWith("u-1", { choice: "replace", pocId: 7, confirmed: true });
  });

  it("PocImport_AlertImportAsNew_SwitchesToNew_AndChooseAnother_GoesBackToTheDropZone", async () => {
    upload.mockResolvedValue(answer({ match: match({ verdict: "ask" }) }));
    await drop();

    fireEvent.click(screen.getByTestId("poc-import-mismatch-new"));
    expect(screen.getByTestId("poc-import-app-new")).toBeChecked();
    expect(screen.getByTestId("poc-import-name")).toHaveValue("shift-planner-v4");
    expect(screen.queryByTestId("poc-import-mismatch")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("poc-import-app-replace"));
    fireEvent.click(screen.getByTestId("poc-import-mismatch-another"));
    expect(screen.getByTestId("poc-import-drop")).toBeInTheDocument();
    expect(screen.queryByTestId("poc-import-checks")).not.toBeInTheDocument();
  });

  it("PocImport_Refusal_ShowsTheServersSentenceAsItCame", async () => {
    const sentence = "Line 4 of the Dockerfile uses RUN --mount, which a PoC may not (pocs.archive dockerfile rules).";
    upload.mockRejectedValue(new ApiResponseError("/api/pocs/uploads", 422, "HTTP 422", sentence));
    render(<PocImportFlow targets={ONE} />);
    fireEvent.change(screen.getByTestId("poc-import-input"), { target: { files: [new File(["zip"], "a.zip")] } });

    expect(await screen.findByTestId("poc-import-refusal")).toHaveTextContent(sentence);
    expect(screen.getByTestId("poc-import-drop")).toBeInTheDocument();
  });

  it("PocImport_SeveralTargets_AsksForOneFirst_AndSendsIt", async () => {
    upload.mockResolvedValue(answer({ target: "lab" }));
    importUpload.mockResolvedValue({ pocId: 9, revision: 1, runId: "r-45" });
    render(<PocImportFlow targets={TWO} />);
    expect(screen.getByTestId("poc-import-pick")).toBeDisabled();

    fireEvent.change(screen.getByTestId("poc-import-target"), { target: { value: "lab" } });
    fireEvent.change(screen.getByTestId("poc-import-input"), { target: { files: [new File(["zip"], "a.zip")] } });
    await screen.findByTestId("poc-import-checks");
    expect(upload).toHaveBeenCalledWith(expect.any(File), "lab");

    fireEvent.click(screen.getByTestId("poc-import-start"));
    await waitFor(() => expect(importUpload).toHaveBeenCalledWith("u-1", { choice: "new", target: "lab", name: "shift-planner-v4" }));
  });
});
