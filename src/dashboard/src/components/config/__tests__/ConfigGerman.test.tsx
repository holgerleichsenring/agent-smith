import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { resetLanguage } from "@/lib/i18n/language";
import { translator } from "@/lib/i18n/translate";
import type { ConnectionRepos, StudioAgent, StudioTracker } from "@/lib/configApi";
import { AgentTuningTab } from "../agent/AgentTuningTab";
import { DiscoveryStatus } from "../DiscoveryStatus";
import { TrackerRouting } from "../TrackerRouting";
import { settingRefusal } from "../settingsRefusal";

// 2026-11-23-5669d: Config Studio in German — the agent's tuning tab, a tracker's routing sentence and a connection's
// discovery line read from the dictionary; config keys, pipeline names and ids stay as written, and a date is written
// in the German locale.

const agent: StudioAgent = { id: "a", provider: "openai", catalog: {}, models: {}, keySecret: null };

describe("Config Studio in German", () => {
  it("AgentTuningTab_German_WordsSectionsAndSeeds", () => {
    resetLanguage("de");
    render(<AgentTuningTab draft={{ ...agent, retry: { maxRetries: 3, initialDelayMs: 1000, backoffMultiplier: 2, maxDelayMs: 30000 } }}
      onChange={vi.fn()} />);
    expect(screen.getByText("Compaction")).toBeInTheDocument();
    expect(screen.getByText("Wiederholung")).toBeInTheDocument();
    expect(screen.getByText("3 Wiederholungen")).toBeInTheDocument();
    expect(screen.getAllByText("nicht gesetzt")).toHaveLength(2);
  });

  it("TrackerRouting_German_KeepsThePipelineName", () => {
    resetLanguage("de");
    const tracker = { id: "jira", type: "jira", authSecret: "T", defaultPipeline: "fix-bug" } as StudioTracker;
    render(<TrackerRouting tracker={tracker} trackerId="jira" />);
    expect(screen.getByText("wie ein Ticket zu einer Pipeline kommt")).toBeInTheDocument();
    expect(screen.getByText("gehört zu Tracker jira — dort bearbeiten")).toBeInTheDocument();
    expect(screen.getByTestId("form-tracker-routing-fallback")).toHaveTextContent(
      "dieser Tracker ordnet kein Label zu, also läuft jedes Ticket, das er hierher leitet, mit fix-bug — was dieser Tracker deklariert.",
    );
  });

  it("DiscoveryStatus_German_WritesTheDateInGerman", () => {
    resetLanguage("de");
    const discovered = {
      repos: [], repoCount: 4, discovering: false, discoveredAt: "2026-11-23T10:05:00Z", lastAttemptAt: null, lastError: null,
    } as unknown as ConnectionRepos;
    render(<DiscoveryStatus testId="d" discovered={discovered} refreshing={false} refreshError={null} onRefresh={vi.fn()} />);
    const at = new Date("2026-11-23T10:05:00Z").toLocaleString("de-DE");
    expect(screen.getByTestId("d-success")).toHaveTextContent(`zuletzt erfolgreich ${at} · 4 Repos`);
    expect(screen.getByTestId("d-refresh")).toHaveTextContent("Jetzt aktualisieren");
  });

  it("settingRefusal_German_NamesBothNumbers", () => {
    const refusal = settingRefusal(translator("de"), "sandbox", { runCommandTimeoutSeconds: 900, stepTimeoutSeconds: 600 } as never);
    expect(refusal).toContain("Ein Run-Command-Standard von 900s darf das Schritt-Limit von 600s nicht übersteigen");
  });
});
