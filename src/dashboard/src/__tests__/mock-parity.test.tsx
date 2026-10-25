import { readFileSync } from "node:fs";
import { Suspense } from "react";
import { render, screen, within, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi } from "vitest";
import JobsPage from "@/app/page";
import RunDetailPage from "@/app/jobs/[id]/page";
import { ConfigStudio } from "@/components/config/ConfigStudio";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { silentEventStore } from "@/lib/eventStore/__tests__/fakes";
import type { OverviewSnapshot, RunSnapshot } from "@/types/hub-events";
import TicketsPage from "@/app/tickets/page";
import { TicketListProvider } from "@/lib/tickets/TicketListProvider";
import { fakeHub, list, pr, ticket } from "@/components/tickets/__tests__/ticketFixtures";
import { TicketPage } from "@/components/tickets/detail/TicketPage";
import { TicketPageStore } from "@/lib/tickets/TicketPageStore";
import { TicketGraph } from "@/components/tickets/graph/TicketGraph";
import { DeliveryView } from "@/components/delivery/DeliveryView";
import { delivery } from "@/components/delivery/__tests__/deliveryFixtures";
import {
  attempt, compute, conversation, defaults, detail, entry, pass, passes, reviewRound, reviews, runDetail, segment, series, spec,
  ticketText,
} from "@/components/tickets/detail/__tests__/detailFixtures";

// p0343c: PIXEL IDENTITY guard — each redesigned surface renders the ratified
// mock's structural DOM (wrapper + key mock classes), bound to real data. If a
// refactor drops the mock DOM, these fail before the operator sees the drift.

const push = vi.fn();
// 2026-10-13-9a59r: the ticket page reads ?tab=, so a case can set the search before rendering.
const search = vi.hoisted(() => ({ current: "" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
  usePathname: () => "/",
  useSearchParams: () => new URLSearchParams(search.current),
}));
// 2026-10-13-9a59r: the ticket composer's Work it out session, at rest — no hub, no server.
vi.mock("@/hooks/useSpecDialog", () => ({
  useSpecDialog: () => ({
    dialogId: "d-1", view: null, conversations: [], entries: [], question: null, proposal: null, filed: null,
    failure: null, uploadNote: null, awaiting: false, working: false, workingSince: null, readings: [], activity: [],
    send: async () => {}, refresh: async () => {}, startNew: async () => {}, open: async () => {}, remove: async () => {},
    attach: async () => {}, attachSite: async () => {},
  }),
}));

function snap(over: Partial<RunSnapshot> = {}): RunSnapshot {
  return {
    runId: "r1",
    pipeline: "fix-bug",
    trigger: "ticket",
    repos: ["server"],
    status: "running",
    prUrl: null,
    summary: null,
    startedAt: "2026-07-17T10:00:00Z",
    finishedAt: null,
    sandboxes: 1,
    stepIndex: 3,
    stepName: "Implement",
    totalSteps: 7,
    lastEventType: null,
    costUsd: 1.5,
    llmCalls: 12,
    ticketId: "T-1",
    ticketTitle: "Fix the login",
    agentName: "azure_openai",
    cancelRequested: false,
    ...over,
  };
}

const overviewRef: { current: OverviewSnapshot | null } = { current: null };
vi.mock("@/hooks/useJobsHub", () => ({
  useJobsHub: () => ({
    // The markdown hooks read the cached artifacts through the hub client —
    // resolve null (not cached) so panels render their honest empty states.
    client: {
      getResultMarkdown: () => Promise.resolve(null),
      getPlanMarkdown: () => Promise.resolve(null),
      getAnalyzeMarkdown: () => Promise.resolve(null),
      getSpecMarkdown: () => Promise.resolve(null), // p0390
    },
    connectionState: 1,
    overview: overviewRef.current,
    systemActivity: null,
  }),
}));
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => [] }));
const tickets = vi.hoisted(() => ({ fetch: vi.fn() }));
vi.mock("@/lib/ticketsApi", () => ({
  fetchTickets: (...args: unknown[]) => tickets.fetch(...args),
  fetchApprovalPreview: () => Promise.resolve({
    project: "shop", ticketId: "101", effect: "continue", reason: "", executed: 1, unstarted: 0, basisRunId: "r-2",
    specs: [{ specId: "s1", goal: "Port", disposition: "kept" }], start: { kind: "start-now" },
  }),
}));
// 2026-10-14-dbcen: the requires graph, a ticket's requires and the done-by-hand mark.
const graphApi = vi.hoisted(() => ({ graph: vi.fn(), requires: vi.fn() }));
vi.mock("@/lib/ticketGraphApi", () => ({
  fetchTicketGraph: (...args: unknown[]) => graphApi.graph(...args),
  fetchTicketRequires: (...args: unknown[]) => graphApi.requires(...args),
  markLandedByHand: vi.fn(),
}));
// 2026-10-15-114fd: closing a ticket.
const closeApi = vi.hoisted(() => ({ close: vi.fn() }));
vi.mock("@/lib/ticketCloseApi", () => ({ closeTicket: (...args: unknown[]) => closeApi.close(...args), reopenTicket: vi.fn() }));
// 2026-10-25-0099c: the Delivery page's one read.
const deliveryApi = vi.hoisted(() => ({ fetch: vi.fn() }));
vi.mock("@/lib/deliveryApi", () => ({ fetchDelivery: (...args: unknown[]) => deliveryApi.fetch(...args) }));
vi.mock("@/hooks/useRunExecutionTree", () => ({
  useRunExecutionTree: () => ({ nodes: [] }),
}));
vi.mock("@/hooks/useRunDetailSnapshot", () => ({
  useRunDetailSnapshot: (_runId: string, list: RunSnapshot | null) => list,
}));

// The studio loads all seven catalogs; give it a small real-shaped fixture.
vi.mock("@/lib/configApi", () => {
  const client = <T,>(rows: T[]) => ({
    list: vi.fn().mockResolvedValue(rows),
    create: vi.fn(),
    update: vi.fn(),
    remove: vi.fn(),
  });
  return {
    agentsApi: client([
      { id: "azure_openai", provider: "Azure OpenAI", catalog: { "gpt-5.1": { model: "gpt-5.1" } }, models: { primary: "gpt-5.1" }, keySecret: "KEY" },
    ]),
    trackersApi: client([{ id: "azdo", type: "Azure DevOps", organization: "o", project: "p", authSecret: "PAT" }]),
    connectionsApi: client([]),
    reposApi: client([{ id: "server", name: "Sample.Server", branch: "main" }]),
    projectsApi: client([
      { id: "sample", agent: "azure_openai", tracker: "azdo", repos: ["server"], pipeline: "fix-bug", pipelines: ["fix-bug"], resolution: null },
    ]),
    mcpServersApi: client([]),
    designSourcesApi: client([]),
    secretsApi: client([{ id: "KEY" }, { id: "PAT" }]),
    fetchChanges: vi.fn().mockResolvedValue([]),
    // 2026-09-22-6968: the catalog load reads the inherited-sandbox projection beside
    // its seven lists; a wholesale module mock has to declare it.
    fetchInheritedSandbox: vi.fn().mockResolvedValue({
      processWide: {
        toolchainImage: { value: null, source: "run-resolved" },
        stepTimeoutSeconds: { value: 900, source: "global-default" },
        runCommandTimeoutSeconds: { value: 300, source: "global-default" },
        agentRegistry: { value: "ghcr.io/example", source: "global-default" },
        agentVersion: { value: "0.50.0", source: "global-default" },
        holdSeconds: { value: 180, source: "code-default" },
        resources: {
          values: { cpuRequest: "250m", cpuLimit: "1000m", memoryRequest: "1Gi", memoryLimit: "4Gi" },
          layer: "global-default",
          noCodeValues: { cpuRequest: "100m", cpuLimit: "500m", memoryRequest: "256Mi", memoryLimit: "1Gi" },
        },
        images: {
          dotnet: { value: "mcr.microsoft.com/dotnet/sdk:9.0", source: "code-default" },
          node: { value: "node:20-bookworm", source: "code-default" },
        },
      },
      projects: {},
    }),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [],
      connectionTypes: [],
      agentProviders: ["Azure OpenAI"],
      resolutionStrategies: ["tag"],
      permissions: [],
      builtInRoles: [],
      pipelines: ["fix-bug"],
    }),
    fetchConnectionRepos: vi.fn().mockResolvedValue({ discoveredAt: null, repos: [] }),
  };
});

// React.use() unwraps an already-instrumented promise synchronously — no
// Suspense round-trip needed in jsdom.
function resolvedParams(id: string): Promise<{ id: string }> {
  const p = Promise.resolve({ id });
  Object.assign(p, { status: "fulfilled", value: { id } });
  return p;
}

const withStore = (node: React.ReactElement) => (
  <EventStoreProvider store={silentEventStore()}>
    {/* the viewer page reads its params via React.use() — needs a boundary */}
    <Suspense fallback={null}>{node}</Suspense>
  </EventStoreProvider>
);

describe("Mock parity", () => {
  it("MockParity_StructuralClasses_PresentPerSurface", async () => {
    // ---- runs home (runs-list.html) ----
    overviewRef.current = {
      active: [
        snap({
          beats: { ticket: "done", plan: "done", building: "active", verify: "pending", outcome: "pending" },
        }),
        snap({
          runId: "r2",
          status: "waiting_for_input",
          pendingQuestion: {
            questionId: "q1",
            type: "Freeform",
            text: "Keep the outbox?",
            context: null,
            choices: ["yes", "no"],
            defaultAnswer: null,
            askedAt: "2026-07-17T10:01:00Z",
            answerDeadlineAt: "2026-07-17T12:00:00Z",
          },
        }),
      ],
      recent: [snap({ runId: "r3", status: "success", finishedAt: "2026-07-17T10:30:00Z" })],
      systemActivity: null,
    };
    const home = render(withStore(<JobsPage />));
    const homeRoot = screen.getByTestId("runs-home");
    expect(homeRoot.className).toContain("mock-shell");
    expect(homeRoot.className).toContain("mock-runs");
    for (const cls of [".main", ".m-head", ".inflow", ".health", ".metric", ".section-head", ".cnt", ".rows", ".rrow", ".spine", ".rrow.st-need"]) {
      expect(homeRoot.querySelector(cls), `runs home missing ${cls}`).not.toBeNull();
    }
    // 2026-10-13-9a59p: a parked run is a row; the answer box lives on the ticket list.
    for (const cls of [".need", ".q-item", ".n-answer"]) {
      expect(homeRoot.querySelector(cls), `runs home still carries ${cls}`).toBeNull();
    }
    home.unmount();

    // ---- ticket list (ticket-view.html, list view) ----
    tickets.fetch.mockResolvedValue(list(
      [
        ticket({
          bucket: "needs_you", kind: "question", status: "waiting_for_input", ticketId: "7",
          pendingQuestion: { runId: "r-101", question: { questionId: "q1", type: "Freeform", text: "Which header style?", context: null, choices: ["IETF draft"], defaultAnswer: null, askedAt: "2026-07-17T10:01:00Z", answerDeadlineAt: "2026-07-17T12:00:00Z" } },
          seriesStrip: { derive: "cut", cells: [{ specId: "s1", state: "done" }, { specId: "s2", state: "asked" }] },
        }),
        ticket({ ticketId: "8", seriesStrip: { derive: "cut", cells: [{ specId: "s1", state: "live" }] }, pullRequests: [pr()] }),
      ],
      { needsYou: 1, working: 1, running: 1 },
    ));
    const ticketView = render(<TicketListProvider client={fakeHub().client}><TicketsPage /></TicketListProvider>);
    const ticketsRoot = screen.getByTestId("tickets-home");
    expect(ticketsRoot.className).toContain("mock-shell");
    expect(ticketsRoot.className).toContain("mock-tickets");
    await screen.findByTestId("ticket-list");
    for (const cls of [".main", ".m-head", ".health", ".metric", ".section-head", ".cnt", ".rows", ".rgroup", ".trow", ".sd", ".rmain", ".tick", ".ttl", ".act", ".strip", ".attempts", ".cost", ".when", ".chev", ".answer", ".q-item", ".q-opt", ".n-answer"]) {
      expect(ticketsRoot.querySelector(cls), `ticket list missing ${cls}`).not.toBeNull();
    }
    ticketView.unmount();

    // ---- ticket detail (ticket-view.html, detail view: Summary) ----
    const carriedAttempt = attempt({ number: 2, runId: "r-2", revision: { number: 2, cause: "comment on the ticket", seriesId: "x", carriedIds: ["s1"], approvalAt: null } });
    const ticketApi = {
      detail: () => Promise.resolve(detail([attempt(), carriedAttempt])),
      series: (_p: string, _t: string, runId: string | null) => Promise.resolve(series(runId ?? "r-2", [
        spec({ state: "carried" }), spec({ specId: "s2", ordinal: 2 }),
      ])),
      conversation: () => Promise.resolve(conversation([])),
      text: () => Promise.resolve(ticketText()),
      passes: (runId: string) => Promise.resolve(passes(runId, { s2: [pass()] })),
      run: () => Promise.reject(new Error("unused")),
      compute: () => Promise.reject(new Error("unused")),
      reviews: () => Promise.resolve(reviews()),
      defaults: () => Promise.resolve(defaults()),
      requires: () => Promise.resolve({ requires: [], requiredBy: [] }),
    };
    const ticketPage = render(
      <TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={ticketApi}>
        <TicketPage />
      </TicketPageStore>,
    );
    const pageRoot = screen.getByTestId("ticket-page");
    expect(pageRoot.className).toContain("mock-shell");
    expect(pageRoot.className).toContain("mock-tickets");
    await screen.findByTestId("attempt-flow");
    for (const cls of [".t-head", ".ticon", ".tsub", ".tabs", ".tab", ".sum", ".sg", ".banner", ".graph", ".g-top", ".gt", ".g-sub", ".lm", ".canvas", ".edge", ".node", ".g-legend", ".sdet", ".sd-h", ".sd-b"]) {
      expect(pageRoot.querySelector(cls), `ticket detail missing ${cls}`).not.toBeNull();
    }
    ticketPage.unmount();

    // ---- run viewer (run-viewer.html) ----
    const viewer = render(withStore(<RunDetailPage params={resolvedParams("r1")} />));
    const viewerRoot = await screen.findByTestId("run-viewer-root");
    expect(viewerRoot.closest(".mock-shell")).not.toBeNull();
    expect(viewerRoot.closest(".mock-viewer")).not.toBeNull();
    expect(viewerRoot.className).toContain("wrap");
    expect(viewerRoot.className).toContain("is-run");
    for (const cls of [".back", ".head-row", ".ident", ".spill", ".storybar", ".beat", ".marker", ".section-head", ".stage", ".grid", ".sidebox", ".trace-btn", ".metric"]) {
      expect(viewerRoot.querySelector(cls), `run viewer missing ${cls}`).not.toBeNull();
    }
    viewer.unmount();

    // ---- config studio (config-studio.html) ----
    render(withStore(<ConfigCatalogProvider><ConfigStudio section="agents" /></ConfigCatalogProvider>));
    const studioRoot = screen.getByTestId("config-studio");
    expect(studioRoot.className).toContain("mock-shell");
    expect(studioRoot.className).toContain("mock-config");
    await screen.findByTestId("config-card-agents-azure_openai");
    for (const cls of [".main", ".m-head", ".btn.primary", ".yaml-note", ".list", ".ecard", ".ec-top", ".ec-ic", ".ec-name", ".tybadge", ".edit-hint", ".fields"]) {
      expect(studioRoot.querySelector(cls), `config studio missing ${cls}`).not.toBeNull();
    }
  });

  it("MockParity_TicketConversationSpecsAcceptance_StructuralClassesPresent", async () => {
    const twoAttempts = [attempt(), attempt({ number: 2, runId: "r-2" })];
    const ticketApi = {
      detail: () => Promise.resolve(detail(twoAttempts)),
      series: (_p: string, _t: string, runId: string | null) => Promise.resolve(series(runId ?? "r-2", [
        spec({ state: "carried", worked: [{ stepId: "1", state: "done", doneByRunId: "r-1", doneByAttempt: 1, doneInPass: 1 }] }),
        spec({ specId: "s2", ordinal: 2, state: "in_progress", done: [{ text: "Works", verdict: null, note: null, citation: null, overrule: null }] }),
      ])),
      conversation: () => Promise.resolve({
        ...conversation([
          entry({ id: "q", attemptRunId: "r-1" }),
          entry({ id: "e", source: "ticket", kind: "edited", role: "person", attemptRunId: "r-2", text: null, detail: { title: { before: "a", after: "b" } } }),
          entry({ id: "c", source: "spec-cut", kind: "cut-review", attemptRunId: "r-2", text: "Deliverable." }),
          entry({ id: "n", source: "conversation", kind: "turn", role: "person", attemptRunId: null, group: "now", text: "Add one" }),
        ]),
        attempts: [{ runId: "r-1", number: 1, cause: null, revision: null }, { runId: "r-2", number: 2, cause: null, revision: null }],
        sessionId: "s-1",
        proposal: { kind: "spec", drafts: [{ specId: "s3", goal: "Import consumer", repos: ["w"], done: ["caps"] }] },
      }),
      text: () => Promise.resolve(ticketText()),
      passes: (runId: string) => Promise.resolve(passes(runId, { s2: [pass()] })),
      run: () => Promise.reject(new Error("unused")),
      compute: () => Promise.reject(new Error("unused")),
      reviews: () => Promise.resolve(reviews({
        attempts: [{ runId: "r-2", number: 2, rounds: [
          reviewRound({ specId: "s2", verdict: "build-with-cuts" }),
          reviewRound({ scope: "series", specId: null, verdict: "refuse" }),
        ] }],
      })),
      defaults: () => Promise.resolve(defaults()),
      requires: () => Promise.resolve({ requires: [], requiredBy: [] }),
    };
    const tabs: Array<[string, string[]]> = [
      ["conversation", [".section-head", ".conv-tools", ".chip", ".conv", ".c-att", ".msg", ".msg.internal", ".av", ".mh", ".where", ".mb", ".diff", ".proposal", ".ph", ".pb", ".composer", ".cp-head", ".cp-actions", ".cp-input"]],
      ["specs", [".sp-grid", ".sp-list", ".sp-item", ".si-top", ".si-cells", ".cell", ".spdoc", ".sh", ".sb", ".blk", ".steps", ".stp", ".crit", ".ci", ".ro-note", ".revs", ".rv",
        // 2026-10-13-8762l: the review rounds block and the series contract review, composed from mock classes.
        ".spdoc .blk .crit .ci .pill.amber", ".section-head .cnt", ".blk .crit .ci .pill.bad"]],
      ["acceptance", [".doc", ".crit", ".ci", ".m", ".cw"]],
    ];
    for (const [tab, classes] of tabs) {
      search.current = `tab=${tab}`;
      const page = render(
        <TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={ticketApi}>
          <TicketPage />
        </TicketPageStore>,
      );
      const root = screen.getByTestId("ticket-page");
      await screen.findByTestId(`ticket-tab-${tab}`);
      await screen.findByText(tab === "conversation" ? "Add one" : tab === "specs" ? "Revisions" : "Acceptance", { selector: tab === "conversation" ? ".mb [data-testid='tracker-text']" : "h2" });
      for (const cls of classes) {
        expect(root.querySelector(cls), `ticket ${tab} tab missing ${cls}`).not.toBeNull();
      }
      page.unmount();
    }
    search.current = "";
  });

  it("MockParity_TicketAttemptsCost_StructuralClassesPresent", async () => {
    const parked = segment({
      endedAt: "2026-09-04T14:10:00Z", endStatus: "waiting_for_input",
      park: { kind: "question", askedAt: "2026-09-04T14:09:00Z", questionId: "1", answeredAt: "2026-09-04T16:00:00Z", answeredBy: "h", resumedAt: "2026-09-04T16:00:00Z" },
    });
    const ticketApi = {
      detail: () => Promise.resolve(detail([attempt(), attempt({ number: 2, runId: "r-2", cost: 9.4 })])),
      series: (_p: string, _t: string, runId: string | null) => Promise.resolve(series(runId ?? "r-2", [spec()])),
      conversation: () => Promise.resolve(conversation([])),
      text: () => Promise.resolve(ticketText()),
      passes: (runId: string) => Promise.resolve(passes(runId, { s1: [pass()] })),
      run: (runId: string) => Promise.resolve(runDetail(runId, { budgetCapUsd: runId === "r-1" ? 5 : 15 })),
      compute: (runId: string) => Promise.resolve(compute(runId, {
        segments: [parked, segment({ startedAt: "2026-09-04T16:00:00Z", endedAt: "2026-09-04T16:30:00Z" })],
      })),
      reviews: () => Promise.resolve(reviews()),
      defaults: () => Promise.resolve(defaults()),
      requires: () => Promise.resolve({ requires: [], requiredBy: [] }),
    };
    const tabs: Array<[string, string, string[]]> = [
      ["attempts", "attempts-timeline", [".next-card", ".bi", ".bt", ".bs", ".section-head", ".tl", ".att", ".dot", ".card", ".a-top", ".a-name", ".an", ".pill", ".rid", ".a-why", ".cause", ".a-num", ".a-body", ".kv", ".k", ".v", ".links", ".chg", ".line"]],
      ["cost", "compute-table", [".costs", ".cr", ".cl", ".track", ".bar", ".parts", ".cv", ".cr.total", ".capmark", ".health.four", ".metric", ".metric.hot", ".banner.need", ".compute", ".c-head", ".c-row", ".c-a", ".c-track", ".c-bar", ".seg", ".park", ".c-figs", ".c-pods", ".pd", ".c-n", ".c-legend"]],
    ];
    for (const [tab, ready, classes] of tabs) {
      search.current = `tab=${tab}`;
      const page = render(
        <TicketPageStore project="shop" ticketId="101" client={fakeHub().client} api={ticketApi}>
          <TicketPage />
        </TicketPageStore>,
      );
      const root = screen.getByTestId("ticket-page");
      await screen.findByTestId(ready);
      await screen.findAllByText(/LLM calls|call time/);
      for (const cls of classes) {
        expect(root.querySelector(cls), `ticket ${tab} tab missing ${cls}`).not.toBeNull();
      }
      page.unmount();
    }
    search.current = "";
  });

  // 2026-10-14-dbcen: ticket-graph.html's list buckets, graph view, never-run page and done-by-hand dialog.
  it("MockParity_TicketGraphSurfaces_CarryTicketGraphHtmlsStructuralClasses", async () => {
    const mock = readFileSync("design/mockups/ticket-graph.html", "utf8");
    const drawn = (root: Element, surface: string, classes: string[]) => {
      for (const cls of classes) {
        expect(root.querySelector(cls), `${surface} missing ${cls}`).not.toBeNull();
        for (const name of cls.split(/[ .]/).filter(Boolean)) expect(mock, `ticket-graph.html lacks ${name}`).toContain(name);
      }
    };
    const neverRun = (ticketId: string, bucket: "blocked" | "filed") => ticket({
      ticketId, ticketKey: `github-${ticketId}`, bucket, status: "", newestRunId: "", attemptCount: 0, cost: 0, attempts: [],
      waitsFor: bucket === "blocked" ? [{ ticketId: "58", project: "shop", key: "#58", title: "#58" }] : [], ticketText: true,
    });

    tickets.fetch.mockResolvedValue(list([neverRun("59", "blocked"), neverRun("61", "filed")], { blocked: 1, filed: 1 }));
    const listView = render(<TicketListProvider client={fakeHub().client}><TicketsPage /></TicketListProvider>);
    await screen.findByTestId("ticket-list");
    drawn(screen.getByTestId("tickets-home"), "list buckets",
      [".m-head", ".viewsw", ".vs", ".section-head", ".cnt.person", ".rows", ".trow", ".sd.blocked", ".sd.filed", ".rmain", ".tick", ".ttl", ".act", ".norun", ".when", ".chev"]);
    listView.unmount();

    graphApi.graph.mockResolvedValue({
      nodes: [
        { id: "a", project: "shop", ticketId: "59", key: "#59", title: "UI", state: "blocked", bucket: "blocked", hasPage: true },
        { id: "b", project: "shop", ticketId: "41", key: "#41", title: "#41", state: "idle", bucket: "idle", hasPage: false },
      ],
      edges: [{ from: "a", to: "b" }],
    });
    const graphView = render(<div className="mock-shell mock-tickets" data-testid="graph-root"><TicketGraph /></div>);
    await screen.findByTestId("ticket-graph");
    fireEvent.click(screen.getByTestId("ticket-graph-node-41"));
    drawn(screen.getByTestId("graph-root"), "graph view",
      [".tgraph", ".tg-canvas", ".tg-edge", ".tg-node.blocked", ".tg-node.idle", ".tg-key", ".tg-title", ".tg-state", ".tg-legend", ".tg-panel", ".pt", ".pr"]);
    graphView.unmount();

    graphApi.requires.mockResolvedValue({
      requires: [{ ticketId: "58", key: "#58", title: "API", state: "ready", hasPage: true }],
      requiredBy: [{ ticketId: "60", key: "#60", title: "Export", state: "blocked", hasPage: true }],
    });
    const summary = neverRun("59", "blocked");
    const ticketApi = {
      detail: () => Promise.resolve(detail([], { summary })),
      series: () => Promise.resolve(series("", [], { runId: null, gate: null, recorded: false })),
      conversation: () => Promise.resolve(conversation([])),
      text: () => Promise.resolve(ticketText()),
      passes: () => Promise.reject(new Error("unused")),
      run: () => Promise.reject(new Error("unused")),
      compute: () => Promise.reject(new Error("unused")),
      reviews: () => Promise.resolve(reviews()),
      defaults: () => Promise.resolve(defaults()),
      requires: () => graphApi.requires(),
    };
    render(
      <TicketPageStore project="shop" ticketId="59" client={fakeHub().client} api={ticketApi}>
        <TicketPage />
      </TicketPageStore>,
    );
    await screen.findByTestId("ticket-waits-for");
    fireEvent.click(screen.getByTestId("done-by-hand-58"));
    drawn(screen.getByTestId("ticket-page"), "never-run page and dialog",
      [".t-head", ".ticon.blocked", ".tsub", ".t-act", ".waits", ".wf-h", ".wf-row", ".wk", ".ws", ".filed-line",
        ".dbh-back", ".dbh", ".dbh-sub", ".dbh-acts"]);
    expect(screen.getByTestId("ticket-page").querySelector(".tabs")).toBeNull();
  });

  // 2026-10-15-114fd: ticket-view.html's closed, failed and PR-closed rows, the Close dialog and the closed header;
  // ticket-graph.html's three new node states and legend entries.
  it("MockParity_TicketClosedSurfaces_CarryTicketViewHtmlsStructuralClasses", async () => {
    const drawnIn = (file: string) => {
      const mock = readFileSync(`design/mockups/${file}`, "utf8");
      return (root: Element, surface: string, classes: string[]) => {
        for (const cls of classes) {
          expect(root.querySelector(cls), `${surface} missing ${cls}`).not.toBeNull();
          for (const name of cls.split(/[ .]/).filter(Boolean)) expect(mock, `${file} lacks ${name}`).toContain(name);
        }
      };
    };
    const drawn = drawnIn("ticket-view.html");
    const ended = (status: string) => [{ runId: "r-1", status, endedOnInfrastructure: false, handedBack: false }];
    const closed = { at: "2026-10-15T08:00:00Z", by: "Ada", note: "superseded", closedPullRequests: [], trackerClosed: false };
    const rows = [
      ticket({ ticketId: "7", bucket: "failed", status: "failed", lastOutcome: "failed", attempts: ended("failed"), pullRequests: [pr()] }),
      ticket({ ticketId: "8", bucket: "pr_closed", status: "success", lastOutcome: "success", attempts: ended("success"), pullRequests: [pr({ state: "closed" })] }),
      ticket({ ticketId: "9", bucket: "closed", kind: "by-hand", status: "failed", lastOutcome: "failed", attempts: ended("failed"), closed }),
    ];
    tickets.fetch.mockResolvedValue(list(rows, { failed: 1, prClosed: 1, closed: 1 }));
    closeApi.close.mockResolvedValue({ kind: "closed", answer: {
      pullRequests: [{ url: "https://example.test/pr/809", closed: true, reason: null }, { url: "https://example.test/pr/810", closed: false, reason: "refused" }],
      tracker: null,
    } });
    const listView = render(<TicketListProvider client={fakeHub().client}><TicketsPage /></TicketListProvider>);
    await screen.findByTestId("ticket-list");
    fireEvent.click(screen.getByTestId("ticket-closed-show"));
    drawn(screen.getByTestId("tickets-home"), "closed buckets",
      [".section-head", ".cnt.red", ".rows", ".trow-wrap", ".trow", ".trow-act", ".sd.bad", ".sd.closed", ".act", ".btn.sm"]);
    fireEvent.click(screen.getByTestId("ticket-close-7"));
    drawn(screen.getByTestId("tickets-home"), "close dialog", [".dbh-back", ".dbh", ".dbh-sub", ".dbh-opt", ".dbh-acts"]);
    fireEvent.click(screen.getByTestId("ticket-close-send"));
    await screen.findByTestId("ticket-close-outcomes");
    drawn(screen.getByTestId("tickets-home"), "close outcomes", [".dbh-outs", ".dbh-out.closed", ".dbh-out.kept"]);
    listView.unmount();

    const ticketApi = {
      detail: () => Promise.resolve(detail([], { summary: { ...rows[2], newestRunId: "", attemptCount: 0, attempts: [] } })),
      series: () => Promise.resolve(series("", [], { runId: null, gate: null, recorded: false })),
      conversation: () => Promise.resolve(conversation([])),
      text: () => Promise.resolve(ticketText()),
      passes: () => Promise.reject(new Error("unused")),
      run: () => Promise.reject(new Error("unused")),
      compute: () => Promise.reject(new Error("unused")),
      reviews: () => Promise.resolve(reviews()),
      defaults: () => Promise.resolve(defaults()),
      requires: () => Promise.resolve({ requires: [], requiredBy: [] }),
    };
    const pageView = render(
      <TicketPageStore project="shop" ticketId="9" client={fakeHub().client} api={ticketApi}>
        <TicketPage />
      </TicketPageStore>,
    );
    await screen.findByTestId("ticket-closed-line");
    drawn(screen.getByTestId("ticket-page"), "closed header", [".t-head", ".ticon.closed", ".tt", ".tsub", ".closed-line", ".t-act"]);
    pageView.unmount();

    const node = (ticketId: string, state: string) =>
      ({ id: ticketId, project: "shop", ticketId, key: `#${ticketId}`, title: "T", state, bucket: "idle" as const, hasPage: true });
    graphApi.graph.mockResolvedValue({
      nodes: [node("60", "blocked"), node("61", "failed"), node("62", "pr_closed"), node("63", "closed")],
      edges: ["61", "62", "63"].map((to) => ({ from: "60", to })),
    });
    render(<div className="mock-shell mock-tickets" data-testid="graph-root"><TicketGraph /></div>);
    await screen.findByTestId("ticket-graph");
    drawnIn("ticket-graph.html")(screen.getByTestId("graph-root"), "graph states",
      [".tg-node.failed", ".tg-node.pr_closed", ".tg-node.closed", ".tg-legend i.failed", ".tg-legend i.pr_closed", ".tg-legend i.closed"]);
  });

  // 2026-10-25-0099c: delivery-overview.html's scope bar, tiles, timeline, breakdowns and tables.
  it("MockParity_DeliverySurface_CarriesDeliveryOverviewHtmlsStructuralClasses", async () => {
    const mock = readFileSync("design/mockups/delivery-overview.html", "utf8");
    deliveryApi.fetch.mockResolvedValue(delivery());
    render(<DeliveryView client={fakeHub().client} />);
    await screen.findByTestId("delivery-kpis");
    const root = screen.getByTestId("delivery-page");
    expect(root.className).toContain("mock-delivery");
    for (const cls of [".seg", ".chips", ".chip", ".kpis", ".kpi", ".panel", ".panel-head", ".legend", ".tl", ".axis", ".ticks",
      ".trow", ".tlabel", ".lane", ".bar.failed", ".bar.running", ".bar.rework", ".mergemark", ".grid2", ".hbar", ".track", ".fill",
      ".tbl-scroll", ".pill", ".did"]) {
      expect(root.querySelector(cls), `delivery missing ${cls}`).not.toBeNull();
      for (const name of cls.split(/[ .]/).filter(Boolean)) expect(mock, `delivery-overview.html lacks ${name}`).toContain(name);
    }
  });

  it("TraceDrawer_HostsExistingMasterDetail", async () => {
    overviewRef.current = {
      active: [snap()],
      recent: [],
      systemActivity: null,
    };
    render(withStore(<RunDetailPage params={resolvedParams("r1")} />));
    await screen.findByTestId("run-viewer-root");

    // The mock's right drawer exists in mock chrome and hosts the EXISTING
    // NavRail + DetailPane master/detail — nothing lost, mock look gained.
    const drawer = screen.getByTestId("trace-drawer");
    expect(drawer.className).toContain("drawer");
    expect(drawer.className).not.toContain("open");
    expect(within(drawer).getByTestId("nav-rail")).toBeInTheDocument();
    expect(within(drawer).getByTestId("trace-master-detail")).toBeInTheDocument();

    fireEvent.click(screen.getByTestId("side-rail-pipeline-jump"));
    expect(screen.getByTestId("trace-drawer").className).toContain("open");
    fireEvent.click(screen.getByTestId("trace-drawer-close"));
    expect(screen.getByTestId("trace-drawer").className).not.toContain("open");
  });

  it("DialogueDrawer_ExistsOnlyForARealPendingQuestion", async () => {
    // Running run, no question → no dialogue affordance anywhere.
    overviewRef.current = { active: [snap()], recent: [], systemActivity: null };
    const noQ = render(withStore(<RunDetailPage params={resolvedParams("r1")} />));
    await screen.findByTestId("run-viewer-root");
    expect(screen.queryByTestId("dialogue-drawer")).not.toBeInTheDocument();
    expect(screen.queryByTestId("side-rail-dialogue")).not.toBeInTheDocument();
    noQ.unmount();

    // Parked run with a REAL question → banner + dialogue drawer hosting the
    // existing PendingQuestionCard in mock chrome.
    overviewRef.current = {
      active: [
        snap({
          status: "waiting_for_input",
          pendingQuestion: {
            questionId: "q1",
            type: "Freeform",
            text: "Keep the outbox?",
            context: null,
            choices: ["yes"],
            defaultAnswer: null,
            askedAt: "2026-07-17T10:01:00Z",
            answerDeadlineAt: "2026-07-17T12:00:00Z",
          },
        }),
      ],
      recent: [],
      systemActivity: null,
    };
    render(withStore(<RunDetailPage params={resolvedParams("r1")} />));
    const banner = await screen.findByTestId("run-banner");
    expect(banner.className).toContain("wait");
    const drawer = screen.getByTestId("dialogue-drawer");
    expect(within(drawer).getByTestId("pending-question-card")).toHaveTextContent("Keep the outbox?");
    fireEvent.click(screen.getByTestId("side-rail-dialogue"));
    expect(screen.getByTestId("dialogue-drawer").className).toContain("open");
  });
});
