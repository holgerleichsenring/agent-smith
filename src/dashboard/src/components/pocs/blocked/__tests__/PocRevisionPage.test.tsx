import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "@/lib/apiResponse";
import type { PocRevisionView, PocScanFinding } from "@/lib/pocsApi";
import { PocRevisionDetail, PocRevisionPage } from "../PocRevisionPage";
import { revisionSteps } from "../StepStrip";

// 2026-11-23-933dc: a revision's page — the blocking findings with their masked line and fix, the notes, the agent's
// explanation as plain text, the overrule box only when the server says the caller may, and its sentence otherwise.

const getRevision = vi.fn();
const overrule = vi.fn();

vi.mock("@/lib/pocsApi", () => ({
  getPocRevision: (...args: unknown[]) => getRevision(...args),
  overrulePocRevision: (...args: unknown[]) => overrule(...args),
}));

function finding(over: Partial<PocScanFinding> = {}): PocScanFinding {
  return {
    id: "osv:GHSA-1|pillow|9.5.0", kind: "vulnerability", ruleId: "osv", file: "requirements.txt", line: 7,
    title: "pillow 9.5.0 has a critical vulnerability", detail: "Arbitrary code execution.", maskedSnippet: null,
    fix: "pillow>=10.2.0", aliases: ["CVE-2023-50447"], ...over,
  };
}

const SECRET = finding({
  id: "sec:abc", kind: "secret", ruleId: "aws-access-key-id", file: "src/config.py", line: 14,
  title: "An AWS access key id is in the code", detail: "", maskedSnippet: 'KEY = "AKIA••••••••••••WXYZ"',
  fix: "Read it from the environment and rotate it.", aliases: [],
});

const NOTE = finding({ id: "note:1", kind: "note", ruleId: "upload-size", title: "Uploads have no size limit", fix: null, aliases: [] });

function view(over: Partial<PocRevisionView> = {}): PocRevisionView {
  return {
    pocId: 4, name: "doc-search", revision: 3, latestRevision: 3, state: "blocked", runId: "r-9", commitSha: null,
    verdict: { blocks: [finding()], notes: [NOTE], isBlocked: true, overrulable: true },
    explanation: "Pillow is only used for thumbnails. <a href=\"https://evil.example\">click</a>",
    mayOverrule: false, whyNot: "a holder of pocs.overrule who can read this PoC's runs can lift this block", ...over,
  };
}

beforeEach(() => {
  getRevision.mockReset();
  overrule.mockReset();
});

describe("PocRevisionDetail", () => {
  it("PocRevisionDetail_Blocked_RendersFindingsNotesAndTheExplanationAsText", () => {
    render(<PocRevisionDetail view={view({ verdict: { blocks: [SECRET, finding()], notes: [NOTE], isBlocked: true, overrulable: false } })} />);

    expect(screen.getByTestId("poc-revision-state")).toHaveTextContent("Blocked by the scan");
    expect(screen.getByTestId("poc-revision-sub")).toHaveTextContent("2 findings stop this revision.");
    expect(screen.getByTestId("poc-finding-sec:abc")).toHaveTextContent("src/config.py:14");
    expect(screen.getByTestId("poc-finding-sec:abc")).toHaveTextContent("Secret");
    expect(screen.getByTestId("poc-finding-sec:abc-snippet")).toHaveTextContent('KEY = "AKIA••••••••••••WXYZ"');
    expect(screen.getByTestId("poc-finding-sec:abc-fix")).toHaveTextContent("rotate it");
    expect(screen.getByTestId("poc-finding-osv:GHSA-1|pillow|9.5.0-aliases")).toHaveTextContent("CVE-2023-50447");
    expect(screen.getByTestId("poc-revision-notes")).toHaveTextContent("1 note that doesn't block");
    expect(screen.getByTestId("poc-note-note:1")).toHaveTextContent("Uploads have no size limit");
    expect(screen.getByTestId("poc-revision-agent")).toHaveTextContent("What the agent read");
    const text = screen.getByTestId("poc-revision-agent-text");
    expect(text).toHaveTextContent('<a href="https://evil.example">click</a>');
    expect(text.querySelector("a")).toBeNull();
    expect(screen.getByTestId("poc-step-codeScan")).toHaveAttribute("data-look", "blocked");
    expect(screen.getByTestId("poc-step-commit")).toHaveAttribute("data-look", "pending");
    expect(screen.getByTestId("poc-revision-import")).toHaveAttribute("href", "/pocs/import");
  });

  it("PocRevisionDetail_WithoutMayOverrule_ShowsWhyNot_AndNoBox", () => {
    render(<PocRevisionDetail view={view({ whyNot: "a secret cannot be overruled: remove it and upload again" })} />);

    expect(screen.queryByTestId("poc-overrule")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-revision-why-not")).toHaveTextContent("a secret cannot be overruled: remove it and upload again");
  });

  it("PocRevisionDetail_WithMayOverrule_ShowsTheBox_AndNoWhyNot", () => {
    render(<PocRevisionDetail view={view({ mayOverrule: true, whyNot: null })} />);

    expect(screen.getByTestId("poc-overrule")).toBeInTheDocument();
    expect(screen.queryByTestId("poc-revision-why-not")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-overrule-submit")).toBeDisabled();
  });

  it("PocRevisionDetail_Overruled_ShowsTheCommitAndNotes_NoOverruleBox", () => {
    render(<PocRevisionDetail view={view({
      state: "overruled", commitSha: "c07d1aa5e9", mayOverrule: true, whyNot: null, explanation: null,
      verdict: { blocks: [], notes: [NOTE], isBlocked: false, overrulable: false },
    })} />);

    expect(screen.getByTestId("poc-revision-sub")).toHaveTextContent("Committed as c07d1aa5e9.");
    expect(screen.getByTestId("poc-note-note:1")).toBeInTheDocument();
    expect(screen.queryByTestId("poc-overrule")).not.toBeInTheDocument();
    expect(screen.queryByTestId("poc-revision-agent")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-step-codeScan")).toHaveAttribute("data-look", "done");
    expect(screen.getByTestId("poc-step-commit")).toHaveTextContent("c07d1aa");
  });

  it("PocRevisionDetail_OlderRevision_LinksTheLatest", () => {
    render(<PocRevisionDetail view={view({ latestRevision: 5 })} />);
    expect(screen.getByTestId("poc-revision-newer").querySelector("a")).toHaveAttribute("href", "/pocs/4/revisions/5");
  });
});

describe("OverruleBox", () => {
  it("OverruleBox_Submit_CallsTheApiWithTheReason_AndShowsTheRun", async () => {
    overrule.mockResolvedValue({ pocId: 4, revision: 3, runId: "r-12" });
    render(<PocRevisionDetail view={view({ mayOverrule: true, whyNot: null })} />);

    fireEvent.change(screen.getByTestId("poc-overrule-reason"), { target: { value: "  demo tomorrow, not reachable  " } });
    expect(screen.getByTestId("poc-overrule-count")).toHaveTextContent("32/500");
    fireEvent.click(screen.getByTestId("poc-overrule-submit"));

    expect(await screen.findByTestId("poc-overrule-run")).toHaveAttribute("href", "/jobs/r-12");
    expect(overrule).toHaveBeenCalledWith(4, 3, "demo tomorrow, not reachable");
  });

  it("OverruleBox_Refusal_ShowsTheServersSentence", async () => {
    const sentence = "a run of doc-search is in progress";
    overrule.mockRejectedValue(new ApiResponseError("/api/pocs/4/revisions/3/overrule", 409, `HTTP 409 — ${sentence}`, sentence));
    render(<PocRevisionDetail view={view({ mayOverrule: true, whyNot: null })} />);

    fireEvent.change(screen.getByTestId("poc-overrule-reason"), { target: { value: "demo" } });
    fireEvent.click(screen.getByTestId("poc-overrule-submit"));

    expect(await screen.findByTestId("poc-overrule-refusal")).toHaveTextContent(sentence);
    expect(screen.getByTestId("poc-overrule")).toBeInTheDocument();
  });
});

describe("PocRevisionPage", () => {
  it("PocRevisionPage_ReadsTheRevision", async () => {
    getRevision.mockResolvedValue(view());
    render(<PocRevisionPage pocId={4} revision={3} />);

    expect(await screen.findByTestId("poc-revision")).toBeInTheDocument();
    expect(getRevision.mock.calls[0].slice(0, 2)).toEqual([4, 3]);
  });

  it("PocRevisionPage_NotFound_SaysSo", async () => {
    getRevision.mockResolvedValue(null);
    render(<PocRevisionPage pocId={4} revision={3} />);

    expect(await screen.findByTestId("poc-revision-missing")).toHaveTextContent("This revision does not exist");
  });

  it("PocRevisionPage_InvalidAddress_IsNotFound_WithoutARead", async () => {
    render(<PocRevisionPage pocId={Number.NaN} revision={3} />);

    await waitFor(() => expect(screen.getByTestId("poc-revision-missing")).toBeInTheDocument());
    expect(getRevision).not.toHaveBeenCalled();
  });
});

describe("revisionSteps", () => {
  it("revisionSteps_Validated_ScannedClean_WithCommit_DoneThroughCommit", () => {
    const steps = revisionSteps(view({
      state: "validated", commitSha: "abc1234", verdict: { blocks: [], notes: [], isBlocked: false, overrulable: false },
    }));
    expect(steps.map((s) => s.look)).toEqual(["done", "done", "done", "pending", "pending", "pending", "pending"]);
  });

  it("revisionSteps_Admitted_AllPending", () => {
    const steps = revisionSteps(view({ state: "admitted", verdict: null }));
    expect(steps.every((s) => s.look === "pending")).toBe(true);
  });
});
