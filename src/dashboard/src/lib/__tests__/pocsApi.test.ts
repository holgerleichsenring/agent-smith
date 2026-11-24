import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiResponseError } from "../apiResponse";
import {
  fetchPocs, getPocDetail, getPocRevision, getPocRevisions, getPocRollout, getPocVariables, importPocUpload, installPocTarget,
  overrulePocRevision, postPocAct, postPocExtend, postPocReminderSeen, putPocVariables, uploadPocZip,
} from "../pocsApi";

// 2026-11-23-933da: GET /api/pocs is the feature switch too — 200 is the list, 404 off, 403 not permitted.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

function plain(status: number, text: string): Response {
  return {
    ok: false, status, text: async () => text, json: async () => { throw new Error("not json"); },
    headers: { get: () => "text/plain" },
  } as unknown as Response;
}

function answer(status: number, body: unknown = ""): Response {
  return {
    ok: status >= 200 && status < 300, status, text: async () => JSON.stringify(body), json: async () => body,
    headers: { get: () => "application/json" },
  } as unknown as Response;
}

afterEach(() => vi.unstubAllGlobals());

describe("fetchPocs", () => {
  it("fetchPocs_200_IsOnWithTheList", async () => {
    const list = { pocs: [], mayImport: true, targets: [{ name: "main", sourceRepo: "poc-source", root: "pocs" }] };
    const fetchMock = vi.fn().mockResolvedValue(answer(200, list));
    vi.stubGlobal("fetch", fetchMock);

    await expect(fetchPocs()).resolves.toEqual({ status: "on", list });
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/pocs$/);
  });

  it("fetchPocs_404_IsOff_403_IsForbidden", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(answer(404)).mockResolvedValueOnce(answer(403, {})));

    await expect(fetchPocs()).resolves.toEqual({ status: "off" });
    await expect(fetchPocs()).resolves.toEqual({ status: "forbidden" });
  });

  it("fetchPocs_500_Throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(answer(500)));

    await expect(fetchPocs()).rejects.toThrow("/api/pocs");
  });
});

// 2026-11-23-933db: the upload is one multipart post (the file, and the target when one is picked); the import is a
// JSON post naming the choice. A refusal is the server's plain sentence, carried whole as the reason.

const UPLOAD = {
  uploadId: "u-1", fileName: "shift-planner.zip", target: "main", port: 8000, entryCount: 12, unpackedBytes: 2048,
  proposedName: "shift-planner", checks: ["paths"], match: null,
};

describe("uploadPocZip", () => {
  it("uploadPocZip_PostsTheFileAndTheTarget_AndReadsTheAnswer", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(200, UPLOAD));
    vi.stubGlobal("fetch", fetchMock);

    await expect(uploadPocZip(new File(["zip"], "shift-planner.zip"), "main")).resolves.toEqual(UPLOAD);

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/uploads$/);
    expect(init.method).toBe("POST");
    const form = init.body as FormData;
    expect((form.get("file") as File).name).toBe("shift-planner.zip");
    expect(form.get("target")).toBe("main");
  });

  it("uploadPocZip_WithoutTarget_SendsNone", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(200, UPLOAD));
    vi.stubGlobal("fetch", fetchMock);

    await uploadPocZip(new File(["zip"], "a.zip"));

    expect((fetchMock.mock.calls[0][1].body as FormData).has("target")).toBe(false);
  });

  it("uploadPocZip_422_ThrowsTheSentenceWhole", async () => {
    const sentence = "'app/.env' is not allowed in a PoC (pocs.archive forbidden names).";
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(plain(422, sentence)));

    const thrown = await uploadPocZip(new File(["zip"], "a.zip")).catch((err: unknown) => err);

    expect(thrown).toBeInstanceOf(ApiResponseError);
    expect((thrown as ApiResponseError).status).toBe(422);
    expect((thrown as ApiResponseError).reason).toBe(sentence);
  });
});

describe("importPocUpload", () => {
  it("importPocUpload_PostsTheChoiceAsJson_AndReadsTheRun", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(201, { pocId: 4, revision: 2, runId: "r-7" }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(importPocUpload("u 1", { choice: "replace", pocId: 4, confirmed: true }))
      .resolves.toEqual({ pocId: 4, revision: 2, runId: "r-7" });

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/uploads\/u%201\/import$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ choice: "replace", pocId: 4, confirmed: true });
  });

  it("importPocUpload_404Empty_ThrowsWithNoReason", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(plain(404, "")));

    const thrown = await importPocUpload("u-1", { choice: "new", name: "a" }).catch((err: unknown) => err);

    expect((thrown as ApiResponseError).status).toBe(404);
    expect((thrown as ApiResponseError).reason).toBeNull();
  });
});

// 2026-11-23-933dc: a revision is read by its PoC and number; a foreign PoC or a caller who may not read its runs gets
// an empty 404, read as not found. The overrule posts a reason; its refusals are plain sentences, carried whole.

const REVISION = {
  pocId: 4, name: "doc-search", revision: 3, latestRevision: 3, state: "blocked", runId: "r-9", commitSha: null,
  verdict: { blocks: [], notes: [], isBlocked: true, overrulable: true }, explanation: null, mayOverrule: true, whyNot: null,
};

describe("getPocRevision", () => {
  it("getPocRevision_200_ReadsTheRevision", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(200, REVISION));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getPocRevision(4, 3)).resolves.toEqual(REVISION);
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/pocs\/4\/revisions\/3$/);
  });

  it("getPocRevision_404Empty_IsNotFound", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(plain(404, "")));

    await expect(getPocRevision(4, 3)).resolves.toBeNull();
  });

  it("getPocRevision_500_Throws", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(answer(500)));

    await expect(getPocRevision(4, 3)).rejects.toThrow("/api/pocs/4/revisions/3");
  });
});

describe("overrulePocRevision", () => {
  it("overrulePocRevision_PostsTheReasonAsJson_AndReadsTheRun", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(201, { pocId: 4, revision: 3, runId: "r-12" }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(overrulePocRevision(4, 3, "demo tomorrow")).resolves.toEqual({ pocId: 4, revision: 3, runId: "r-12" });

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/4\/revisions\/3\/overrule$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ reason: "demo tomorrow" });
  });

  it("overrulePocRevision_TextRefusal_ThrowsTheSentenceWhole", async () => {
    const sentence = "a secret cannot be overruled: remove it and upload again";
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(plain(400, sentence)));

    const thrown = await overrulePocRevision(4, 3, "demo").catch((err: unknown) => err);

    expect(thrown).toBeInstanceOf(ApiResponseError);
    expect((thrown as ApiResponseError).status).toBe(400);
    expect((thrown as ApiResponseError).reason).toBe(sentence);
  });

  it("overrulePocRevision_409And410_CarryTheirSentences", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(plain(409, "a run of doc-search is in progress"))
      .mockResolvedValueOnce(plain(410, "the upload of r3 was purged after 7 days (pocs.blocked_retention_days)")));

    const busy = await overrulePocRevision(4, 3, "x").catch((err: unknown) => err) as ApiResponseError;
    const gone = await overrulePocRevision(4, 3, "x").catch((err: unknown) => err) as ApiResponseError;

    expect([busy.status, busy.reason]).toEqual([409, "a run of doc-search is in progress"]);
    expect([gone.status, gone.reason]).toEqual([410, "the upload of r3 was purged after 7 days (pocs.blocked_retention_days)"]);
  });
});

// 2026-11-23-933dd: the Install action of a PoC target.
describe("installPocTarget", () => {
  it("installPocTarget_PostsToTheTarget_AndReadsTheRunsOutcome", async () => {
    const said = { target: "main", succeeded: true, message: "commit abc123", runId: "r-1" };
    const fetchMock = vi.fn().mockResolvedValue(answer(200, said));
    vi.stubGlobal("fetch", fetchMock);

    await expect(installPocTarget("main")).resolves.toEqual(said);

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/targets\/main\/install$/);
    expect(init.method).toBe("POST");
  });
});

// 2026-11-23-933dg: Start carries the variables; GET lists a PoC's variables (no secret value), PUT sets and deletes them
// and answers the run's line; refusals are plain sentences, carried whole.
describe("PoC variables", () => {
  it("importPocUpload_SendsTheVariablesInTheBody", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(201, { pocId: 4, revision: 1, runId: "r-8" }));
    vi.stubGlobal("fetch", fetchMock);
    const variables = [{ name: "API_KEY", secret: true, value: "s3cr3t-value" }, { name: "MODE", secret: false, value: "demo" }];

    await importPocUpload("u-1", { choice: "new", target: "main", name: "rota", variables });

    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({ choice: "new", target: "main", name: "rota", variables });
  });

  it("getPocVariables_ReadsTheList", async () => {
    const list = { variables: [
      { name: "API_KEY", kind: "secret", setBy: "dev-one", setAt: "2026-11-24T08:00:00Z", value: null },
      { name: "MODE", kind: "plain", setBy: "dev-one", setAt: "2026-11-24T08:00:00Z", value: "demo" },
    ] };
    const fetchMock = vi.fn().mockResolvedValue(answer(200, list));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getPocVariables(4)).resolves.toEqual(list);
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/pocs\/4\/variables$/);
  });

  it("putPocVariables_PutsSetAndDelete_AndReadsTheLine", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(200, { runId: "r-9", line: "1 set, 1 deleted" }));
    vi.stubGlobal("fetch", fetchMock);
    const body = { set: [{ name: "MODE", secret: false, value: "live" }], delete: ["OLD_KEY"] };

    await expect(putPocVariables(4, body)).resolves.toEqual({ runId: "r-9", line: "1 set, 1 deleted" });

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/4\/variables$/);
    expect(init.method).toBe("PUT");
    expect(JSON.parse(init.body as string)).toEqual(body);
  });

  it("putPocVariables_409_ThrowsTheSentenceWhole", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(plain(409, "run r-3 of doc-search is still running")));

    const thrown = await putPocVariables(4, { set: [], delete: ["A"] }).catch((err: unknown) => err) as ApiResponseError;

    expect([thrown.status, thrown.reason]).toEqual([409, "run r-3 of doc-search is still running"]);
  });
});

// 2026-11-23-c81ec: one rollout — the view, or null on the empty 404 a foreign PoC or run answers.
describe("getPocRollout", () => {
  it("getPocRollout_200_IsTheView_404_IsNull", async () => {
    const view = {
      pocId: 4, name: "todo", runId: "r-7", revision: 2, state: "ready", rolloutState: "healthy", rolloutAt: null,
      url: "https://todo.poc.example.internal", pipelineUrl: null, sourceCommitSha: null, gitopsCommitSha: null, gitopsCommitUrl: null,
    };
    const fetchMock = vi.fn().mockResolvedValueOnce(answer(200, view)).mockResolvedValueOnce(answer(404));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getPocRollout(4, "r-7")).resolves.toEqual(view);
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/pocs\/4\/rollouts\/r-7$/);
    await expect(getPocRollout(4, "r-x")).resolves.toBeNull();
  });
});

// 2026-11-23-c81ed: the detail answer and the revisions list read an empty 404 as null; an act posts its body as JSON and
// a refusal carries the server's sentence whole; a variable write answers the apply run it queued, or why not.
describe("PoC detail and acts", () => {
  it("getPocDetail_200_IsTheDetail_404_IsNull", async () => {
    const poc = { id: 4, name: "todo", acts: { stop: true, start: false, rollback: false, apply: true, delete: true } };
    const fetchMock = vi.fn().mockResolvedValueOnce(answer(200, poc)).mockResolvedValueOnce(answer(404));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getPocDetail(4)).resolves.toEqual(poc);
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/pocs\/4$/);
    await expect(getPocDetail(5)).resolves.toBeNull();
  });

  it("getPocRevisions_200_IsTheList_404_IsNull", async () => {
    const list = { revisions: [{ number: 2, state: "built", digest: null, rolloutState: "healthy", rolloutAt: null, runId: "r-2",
      commitSha: null, mayRollBack: true, current: false }] };
    const fetchMock = vi.fn().mockResolvedValueOnce(answer(200, list)).mockResolvedValueOnce(answer(404));
    vi.stubGlobal("fetch", fetchMock);

    await expect(getPocRevisions(4)).resolves.toEqual(list);
    expect(String(fetchMock.mock.calls[0][0])).toMatch(/\/api\/pocs\/4\/revisions$/);
    await expect(getPocRevisions(5)).resolves.toBeNull();
  });

  it("postPocAct_PostsTheBodyToTheAct_AndReadsTheRun", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(201, { pocId: 4, act: "delete", runId: "r-21" }));
    vi.stubGlobal("fetch", fetchMock);

    await expect(postPocAct(4, "delete", { confirmName: "todo" })).resolves.toEqual({ pocId: 4, act: "delete", runId: "r-21" });

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/4\/acts\/delete$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(init.body as string)).toEqual({ confirmName: "todo" });
  });

  it("postPocAct_NoBody_PostsAnEmptyObject", async () => {
    const fetchMock = vi.fn().mockResolvedValue(answer(201, { pocId: 4, act: "stop", runId: "r-20" }));
    vi.stubGlobal("fetch", fetchMock);

    await postPocAct(4, "stop");

    expect(JSON.parse(fetchMock.mock.calls[0][1].body as string)).toEqual({});
  });

  it("postPocAct_409And404_CarryTheSentenceOrNone", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(plain(409, "a stopped PoC cannot be stopped"))
      .mockResolvedValueOnce(plain(404, "")));

    const busy = await postPocAct(4, "stop").catch((err: unknown) => err) as ApiResponseError;
    const gone = await postPocAct(4, "stop").catch((err: unknown) => err) as ApiResponseError;

    expect([busy.status, busy.reason]).toEqual([409, "a stopped PoC cannot be stopped"]);
    expect([gone.status, gone.reason]).toEqual([404, null]);
  });

  it("putPocVariables_ReadsTheApplyRunAndNote", async () => {
    const said = { runId: "r-9", line: "1 set", applyRunId: null, applyNote: "a run of todo is live" };
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(answer(200, said)));

    await expect(putPocVariables(4, { set: [], delete: ["A"] })).resolves.toEqual(said);
  });
});

// 2026-11-23-c81ee: the reminder-seen post and the extend act.
describe("postPocReminderSeen", () => {
  it("postPocReminderSeen_PostsWithoutABody_To204", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ...answer(204), text: async () => "" });
    vi.stubGlobal("fetch", fetchMock);

    await expect(postPocReminderSeen(4)).resolves.toBeUndefined();

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/4\/reminders\/seen$/);
    expect(init.method).toBe("POST");
    expect(init.body).toBeUndefined();
  });
});

describe("postPocExtend", () => {
  it("postPocExtend_PostsToTheExtendAct_AndReadsTheNewExpiry", async () => {
    const said = { pocId: 4, runId: "r-40", expiresAt: "2027-03-01T00:00:00Z", extensions: 1 };
    const fetchMock = vi.fn().mockResolvedValue(answer(200, said));
    vi.stubGlobal("fetch", fetchMock);

    await expect(postPocExtend(4)).resolves.toEqual(said);

    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/pocs\/4\/acts\/extend$/);
    expect(init.method).toBe("POST");
  });

  it("postPocExtend_409And422_CarryTheServersSentence", async () => {
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(plain(409, "run r-3 of todo is still running"))
      .mockResolvedValueOnce(plain(422, "todo was extended 2 times; pocs.lifetime.max_owner_extensions is 2")));

    const busy = await postPocExtend(4).catch((err: unknown) => err) as ApiResponseError;
    const refusedRun = await postPocExtend(4).catch((err: unknown) => err) as ApiResponseError;

    expect([busy.status, busy.reason]).toEqual([409, "run r-3 of todo is still running"]);
    expect(refusedRun.status).toBe(422);
    expect(refusedRun.reason).toContain("pocs.lifetime.max_owner_extensions");
  });
});
