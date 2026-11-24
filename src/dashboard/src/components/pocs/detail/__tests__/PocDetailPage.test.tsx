import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "@/lib/apiResponse";
import type { PocDetail, PocRevisionRow, PocVariableView } from "@/lib/pocsApi";
import { PocDetailPage } from "../PocDetailPage";
import { variablesChange } from "../PocDetailVariables";
import { shortDigest } from "../DesiredState";
import { readinessHints } from "../ReadinessSetting";

// 2026-11-23-c81ed: the PoC detail page enables only the acts the server's answer admits, asks for the name before a
// delete, links each revision's Rollout page with Roll back only where the answer allows it, mounts the variables
// table, and posts apply with the readiness probe chosen.

const getDetail = vi.fn();
const getRevisions = vi.fn();
const postAct = vi.fn();
const getVariables = vi.fn();
const putVariables = vi.fn();

vi.mock("@/lib/pocsApi", () => ({
  getPocDetail: (...args: unknown[]) => getDetail(...args),
  getPocRevisions: (...args: unknown[]) => getRevisions(...args),
  postPocAct: (...args: unknown[]) => postAct(...args),
  getPocVariables: (...args: unknown[]) => getVariables(...args),
  putPocVariables: (...args: unknown[]) => putVariables(...args),
}));

const DIGEST = "sha256:" + "4b1e".repeat(16);

function detail(over: Partial<PocDetail> = {}): PocDetail {
  return {
    id: 4, name: "todo", project: "poc-ann", target: "main", host: "todo.poc.example.internal",
    url: "https://todo.poc.example.internal", state: "ready", ownerName: "Ann", lastRunId: "r-9",
    desired: { replicas: 1, revision: 3, imageDigest: DIGEST, readinessPath: "/health" },
    deletePending: false, mayAct: true,
    acts: { stop: true, start: false, rollback: true, apply: true, delete: true },
    readinessPathMaxChars: 200,
    audit: [
      { at: "2026-11-23T10:00:00Z", actor: "Ann", act: "imported", reason: null, runId: "r-1" },
      { at: "2026-11-23T11:00:00Z", actor: "Ann", act: "stopped", reason: null, runId: null },
    ],
    ...over,
  };
}

function revision(number: number, over: Partial<PocRevisionRow> = {}): PocRevisionRow {
  return {
    number, state: "built", digest: DIGEST, rolloutState: "healthy", rolloutAt: "2026-11-23T10:05:00Z", runId: `r-${number}`,
    commitSha: "1111111aaaaaaa", mayRollBack: false, current: false, ...over,
  };
}

const REVISIONS = [
  revision(3, { current: true }),
  revision(2, { mayRollBack: true }),
  revision(1, { rolloutState: "failed", runId: null }),
];

beforeEach(() => {
  for (const mock of [getDetail, getRevisions, postAct, getVariables, putVariables]) mock.mockReset();
  getDetail.mockResolvedValue(detail());
  getRevisions.mockResolvedValue({ revisions: REVISIONS });
  getVariables.mockResolvedValue({ variables: [] });
});

async function shown() {
  render(<PocDetailPage pocId={4} />);
  return screen.findByTestId("poc-detail");
}

describe("PocDetailPage", () => {
  it("PocDetailPage_Header_ShowsNameUrlStateAndDesiredState", async () => {
    await shown();
    expect(screen.getByTestId("poc-detail-header")).toHaveTextContent("todo");
    expect(screen.getByTestId("poc-detail-url")).toHaveAttribute("href", "https://todo.poc.example.internal");
    expect(screen.getByTestId("poc-detail-state")).toHaveTextContent("Ready");
    expect(screen.queryByTestId("poc-detail-delete-pending")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-detail-desired-replicas")).toHaveTextContent("1");
    expect(screen.getByTestId("poc-detail-desired-revision")).toHaveTextContent("r3");
    expect(screen.getByTestId("poc-detail-desired-digest")).toHaveTextContent(shortDigest(DIGEST));
  });

  it("PocDetailPage_DeletePending_ShowsTheBadge", async () => {
    getDetail.mockResolvedValue(detail({ deletePending: true }));
    await shown();
    expect(screen.getByTestId("poc-detail-delete-pending")).toHaveTextContent("Delete pending");
  });

  it("PocDetailPage_404_ShowsNotFound", async () => {
    getDetail.mockResolvedValue(null);
    getRevisions.mockResolvedValue(null);
    render(<PocDetailPage pocId={4} />);
    expect(await screen.findByTestId("poc-detail-missing")).toHaveTextContent("does not exist");
  });

  it("PocDetailPage_EnablesOnlyTheActsTheAnswerAdmits", async () => {
    await shown();
    expect(screen.getByTestId("poc-detail-act-stop")).toBeEnabled();
    expect(screen.getByTestId("poc-detail-act-start")).toBeDisabled();
    expect(screen.getByTestId("poc-detail-act-apply")).toBeEnabled();
    expect(screen.getByTestId("poc-detail-act-delete")).toBeEnabled();
  });

  it("PocDetailPage_MayNotAct_EverythingDisabled", async () => {
    getDetail.mockResolvedValue(detail({
      mayAct: false, acts: { stop: false, start: false, rollback: false, apply: false, delete: false },
    }));
    await shown();
    for (const act of ["stop", "start", "apply", "delete"]) expect(screen.getByTestId(`poc-detail-act-${act}`)).toBeDisabled();
    expect(screen.queryByTestId("poc-detail-revision-2-rollback")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-detail-readiness-save")).toBeDisabled();
  });

  it("PocDetailPage_Stop_PostsTheAct_LinksTheRolloutAndReadsAgain", async () => {
    postAct.mockResolvedValue({ pocId: 4, act: "stop", runId: "r-20" });
    await shown();
    fireEvent.click(screen.getByTestId("poc-detail-act-stop"));

    expect(await screen.findByTestId("poc-detail-acted-run")).toHaveAttribute("href", "/pocs/4/rollouts/r-20");
    expect(postAct).toHaveBeenCalledWith(4, "stop", {});
    await waitFor(() => expect(getDetail).toHaveBeenCalledTimes(2));
    expect(getRevisions).toHaveBeenCalledTimes(2);
  });

  it("PocDetailPage_ActRefused_ShowsTheServersSentence", async () => {
    postAct.mockRejectedValue(new ApiResponseError("/api/pocs/4/acts/stop", 409, "HTTP 409", "run r-3 of todo is still running"));
    await shown();
    fireEvent.click(screen.getByTestId("poc-detail-act-stop"));
    expect(await screen.findByTestId("poc-detail-refusal")).toHaveTextContent("run r-3 of todo is still running");
  });

  it("PocDetailPage_Delete_RequiresTypingTheName_AndPostsIt", async () => {
    postAct.mockResolvedValue({ pocId: 4, act: "delete", runId: "r-21" });
    await shown();
    fireEvent.click(screen.getByTestId("poc-detail-act-delete"));
    const confirm = screen.getByTestId("poc-detail-delete-confirm");
    expect(confirm).toBeDisabled();

    fireEvent.change(screen.getByTestId("poc-detail-delete-name"), { target: { value: "tod" } });
    expect(confirm).toBeDisabled();
    fireEvent.change(screen.getByTestId("poc-detail-delete-name"), { target: { value: "todo" } });
    expect(confirm).toBeEnabled();
    fireEvent.click(confirm);

    await waitFor(() => expect(postAct).toHaveBeenCalledWith(4, "delete", { confirmName: "todo" }));
    expect(await screen.findByTestId("poc-detail-acted-run")).toHaveAttribute("href", "/pocs/4/rollouts/r-21");
    await waitFor(() => expect(screen.queryByTestId("poc-detail-delete-dialog")).not.toBeInTheDocument());
  });

  it("PocDetailPage_Revisions_LinkTheRolloutPages_RollBackOnlyWhereAllowed", async () => {
    postAct.mockResolvedValue({ pocId: 4, act: "rollback", runId: "r-22" });
    await shown();
    expect(screen.getByTestId("poc-detail-revision-3-rollout-link")).toHaveAttribute("href", "/pocs/4/rollouts/r-3");
    expect(screen.getByTestId("poc-detail-revision-2-rollout-link")).toHaveAttribute("href", "/pocs/4/rollouts/r-2");
    expect(screen.queryByTestId("poc-detail-revision-1-rollout-link")).not.toBeInTheDocument();
    expect(screen.getByTestId("poc-detail-revision-3-current")).toBeInTheDocument();
    expect(screen.getByTestId("poc-detail-revision-1-rollout")).toHaveTextContent("Failed");
    expect(screen.queryByTestId("poc-detail-revision-3-rollback")).not.toBeInTheDocument();
    expect(screen.queryByTestId("poc-detail-revision-1-rollback")).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId("poc-detail-revision-2-rollback"));
    await waitFor(() => expect(postAct).toHaveBeenCalledWith(4, "rollback", { revision: 2 }));
  });

  it("PocDetailPage_RollbackNotAdmitted_HidesRollBack", async () => {
    getDetail.mockResolvedValue(detail({ acts: { stop: true, start: false, rollback: false, apply: true, delete: true } }));
    await shown();
    expect(screen.queryByTestId("poc-detail-revision-2-rollback")).not.toBeInTheDocument();
  });

  it("PocDetailPage_Readiness_PostsApplyWithTcpOrThePath", async () => {
    postAct.mockResolvedValue({ pocId: 4, act: "apply", runId: "r-23" });
    await shown();
    const save = screen.getByTestId("poc-detail-readiness-save");
    expect(save).toBeDisabled(); // unchanged

    fireEvent.change(screen.getByTestId("poc-detail-readiness-path"), { target: { value: "ready" } });
    expect(screen.getByTestId("poc-detail-readiness-hint")).toHaveTextContent('starts with "/"');
    expect(save).toBeDisabled();
    fireEvent.change(screen.getByTestId("poc-detail-readiness-path"), { target: { value: "/ready" } });
    fireEvent.click(save);
    await waitFor(() => expect(postAct).toHaveBeenCalledWith(4, "apply", { readiness: "/ready" }));

    fireEvent.click(screen.getByTestId("poc-detail-readiness-tcp"));
    await waitFor(() => expect(screen.getByTestId("poc-detail-readiness-save")).toBeEnabled());
    fireEvent.click(screen.getByTestId("poc-detail-readiness-save"));
    await waitFor(() => expect(postAct).toHaveBeenLastCalledWith(4, "apply", { readiness: "tcp" }));
  });

  it("PocDetailPage_Variables_TableMounted_SaveShowsTheApplyRun", async () => {
    getVariables.mockResolvedValue({ variables: [
      { name: "API_KEY", kind: "secret", setBy: "Ann", setAt: "2026-11-23T10:00:00Z", value: null },
      { name: "MODE", kind: "plain", setBy: "Ann", setAt: "2026-11-23T10:00:00Z", value: "demo" },
    ] });
    putVariables.mockResolvedValue({ runId: "r-30", line: "1 set", applyRunId: "r-31", applyNote: null });
    await shown();
    expect(await screen.findByTestId("poc-detail-variables-table")).toBeInTheDocument();
    expect(screen.getByTestId("poc-detail-variables-table-row-1-value")).toHaveValue("demo");
    expect(screen.getByTestId("poc-detail-variables-save")).toBeDisabled();

    fireEvent.change(screen.getByTestId("poc-detail-variables-table-row-1-value"), { target: { value: "live" } });
    fireEvent.click(screen.getByTestId("poc-detail-variables-save"));

    await waitFor(() => expect(putVariables).toHaveBeenCalledWith(4, {
      set: [{ name: "MODE", secret: false, value: "live" }], delete: [],
    }));
    expect(await screen.findByTestId("poc-detail-variables-apply-run")).toHaveAttribute("href", "/jobs/r-31");
  });

  it("PocDetailPage_Variables_ApplyNotQueued_ShowsTheNote", async () => {
    getVariables.mockResolvedValue({ variables: [
      { name: "MODE", kind: "plain", setBy: "Ann", setAt: "2026-11-23T10:00:00Z", value: "demo" },
    ] });
    putVariables.mockResolvedValue({ runId: "r-30", line: "1 deleted", applyRunId: null, applyNote: "a run of todo is live" });
    await shown();
    fireEvent.click(await screen.findByTestId("poc-detail-variables-table-row-0-remove"));
    fireEvent.click(screen.getByTestId("poc-detail-variables-save"));
    expect(await screen.findByTestId("poc-detail-variables-apply-note")).toHaveTextContent("a run of todo is live");
    expect(putVariables).toHaveBeenCalledWith(4, { set: [], delete: ["MODE"] });
  });

  it("PocDetailPage_Audit_ListsEntriesWithTheirRolloutLinks", async () => {
    await shown();
    expect(screen.getByTestId("poc-detail-audit-0")).toHaveTextContent("Imported");
    expect(screen.getByTestId("poc-detail-audit-0-run")).toHaveAttribute("href", "/pocs/4/rollouts/r-1");
    expect(screen.getByTestId("poc-detail-audit-1")).toHaveTextContent("Stopped");
    expect(screen.queryByTestId("poc-detail-audit-1-run")).not.toBeInTheDocument();
  });
});

describe("variablesChange", () => {
  const stored: PocVariableView[] = [
    { name: "API_KEY", kind: "secret", setBy: "Ann", setAt: "x", value: null },
    { name: "MODE", kind: "plain", setBy: "Ann", setAt: "x", value: "demo" },
  ];

  it("variablesChange_EmptySecretUnchangedPlain_SendsNothing", () => {
    expect(variablesChange(stored, [
      { name: "API_KEY", secret: true, value: "" }, { name: "MODE", secret: false, value: "demo" },
    ])).toEqual({ set: [], delete: [] });
  });

  it("variablesChange_NewSecretValueAndRemovedName_SetAndDelete", () => {
    expect(variablesChange(stored, [{ name: "API_KEY", secret: true, value: "n3w-secret" }])).toEqual({
      set: [{ name: "API_KEY", secret: true, value: "n3w-secret" }], delete: ["MODE"],
    });
  });

  it("variablesChange_KindChanged_IsSet", () => {
    expect(variablesChange(stored, [
      { name: "API_KEY", secret: true, value: "" }, { name: "MODE", secret: true, value: "secret-mode" },
    ]).set).toEqual([{ name: "MODE", secret: true, value: "secret-mode" }]);
  });
});

describe("readinessHints", () => {
  it("readinessHints_NoSlashOrTooLong_Hints", () => {
    expect(readinessHints("/health", 10)).toEqual([]);
    expect(readinessHints("health", 10)).toEqual(["pocs.detail.readiness.hint.slash"]);
    expect(readinessHints("/" + "a".repeat(10), 10)).toEqual(["pocs.detail.readiness.hint.length"]);
  });
});

describe("shortDigest", () => {
  it("shortDigest_CutsTheHash", () => {
    expect(shortDigest("sha256:" + "a".repeat(64))).toBe("sha256:" + "a".repeat(12) + "…");
    expect(shortDigest("sha256:abc")).toBe("sha256:abc");
  });
});
