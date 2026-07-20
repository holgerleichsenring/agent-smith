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

// p0343c: PIXEL IDENTITY guard — each redesigned surface renders the ratified
// mock's structural DOM (wrapper + key mock classes), bound to real data. If a
// refactor drops the mock DOM, these fail before the operator sees the drift.

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push }),
  usePathname: () => "/",
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
    },
    connectionState: 1,
    overview: overviewRef.current,
    systemActivity: null,
  }),
}));
vi.mock("@/hooks/useRunEvents", () => ({ useRunEvents: () => [] }));
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
      { id: "azure_openai", provider: "Azure OpenAI", models: { coding: { model: "gpt-5.1" } }, keySecret: "KEY" },
    ]),
    trackersApi: client([{ id: "azdo", type: "Azure DevOps", organization: "o", project: "p", authSecret: "PAT" }]),
    connectionsApi: client([]),
    reposApi: client([{ id: "server", name: "Sample.Server", branch: "main" }]),
    projectsApi: client([
      { id: "sample", agent: "azure_openai", tracker: "azdo", repos: ["server"], pipeline: "fix-bug", pipelines: ["fix-bug"], resolution: null },
    ]),
    mcpServersApi: client([]),
    secretsApi: client([{ id: "KEY" }, { id: "PAT" }]),
    fetchChanges: vi.fn().mockResolvedValue([]),
    revertChange: vi.fn(),
    fetchConfigExportYml: vi.fn(),
    fetchCapabilities: vi.fn().mockResolvedValue({
      trackerTypes: [],
      connectionTypes: [],
      agentProviders: ["Azure OpenAI"],
      resolutionStrategies: ["tag"],
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
    for (const cls of [".main", ".m-head", ".inflow", ".health", ".metric", ".section-head", ".cnt", ".rows", ".rrow", ".spine", ".need", ".n-top", ".q-item", ".q-opt", ".n-answer"]) {
      expect(homeRoot.querySelector(cls), `runs home missing ${cls}`).not.toBeNull();
    }
    home.unmount();

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
