import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { ConnectionsView } from "../ConnectionsView";
import * as api from "@/lib/diagnosticsApi";

vi.mock("@/lib/diagnosticsApi", () => ({
  fetchConnections: vi.fn(),
  probeConnection: vi.fn(),
}));

const mockedApi = api as unknown as {
  fetchConnections: ReturnType<typeof vi.fn>;
  probeConnection: ReturnType<typeof vi.fn>;
};

describe("ConnectionsView", () => {
  beforeEach(() => {
    mockedApi.fetchConnections.mockReset();
    mockedApi.probeConnection.mockReset();
  });

  it("ConnectionsView_ListsConnection_WithUnknownPillUntilTested", async () => {
    mockedApi.fetchConnections.mockResolvedValue({
      connections: [{ name: "agent-smith", type: "GitHub", kind: "repo", category: "service" }],
      webhooks: [],
    });

    render(<ConnectionsView />);

    expect(await screen.findByTestId("connection-row-agent-smith")).toBeInTheDocument();
    expect(screen.getByTestId("status-pill-unknown")).toBeInTheDocument();
  });

  it("ConnectionsView_TestClick_ProbesAndShowsOkPill", async () => {
    mockedApi.fetchConnections.mockResolvedValue({
      connections: [{ name: "agent-smith", type: "GitHub", kind: "repo", category: "service" }],
      webhooks: [],
    });
    mockedApi.probeConnection.mockResolvedValue({
      name: "agent-smith", type: "GitHub", kind: "repo", category: "service", ok: true, latencyMs: 42, error: null,
    });

    render(<ConnectionsView />);
    fireEvent.click(await screen.findByTestId("connection-test-agent-smith"));

    expect(await screen.findByTestId("status-pill-ok")).toBeInTheDocument();
    expect(mockedApi.probeConnection).toHaveBeenCalledWith("agent-smith");
  });

  it("ConnectionsView_GroupsByCategory_AgentGroupCarriesCostNote", async () => {
    mockedApi.fetchConnections.mockResolvedValue({
      connections: [
        { name: "agent-smith", type: "GitHub", kind: "repo", category: "service" },
        { name: "claude-default", type: "claude", kind: "agent", category: "agent" },
        { name: "redis", type: "Redis", kind: "redis", category: "infra" },
      ],
      webhooks: [],
    });

    render(<ConnectionsView />);

    expect(await screen.findByTestId("connection-row-claude-default")).toBeInTheDocument();
    expect(screen.getByTestId("connection-group-Agents")).toHaveTextContent("minimal (1-token) LLM call");
    expect(screen.getByTestId("connection-row-redis")).toBeInTheDocument();
  });

  it("ConnectionsView_WebhookPanel_ShowsSecretLastSeenAndCannotTestNote", async () => {
    mockedApi.fetchConnections.mockResolvedValue({
      connections: [],
      webhooks: [
        {
          platform: "github",
          secretConfigured: true,
          lastReceivedUtc: "2026-06-01T00:00:00Z",
          acceptedUnsignedDelivery: false,
        },
      ],
    });

    render(<ConnectionsView />);

    expect(await screen.findByTestId("webhook-row-github")).toHaveTextContent("secret configured");
    expect(screen.getByTestId("webhook-panel")).toHaveTextContent("cannot be actively tested");
    expect(screen.queryByTestId("webhook-unsigned-github")).not.toBeInTheDocument();
  });

  // p0506: the conjunction — a delivery landed and nothing verified it. A deployment
  // that never received one says nothing, so a polling-only install stays quiet.
  it("ConnectionsView_WebhookAcceptedUnsigned_NamesTheExposure", async () => {
    mockedApi.fetchConnections.mockResolvedValue({
      connections: [],
      webhooks: [
        {
          platform: "github",
          secretConfigured: false,
          lastReceivedUtc: "2026-06-01T00:00:00Z",
          acceptedUnsignedDelivery: true,
        },
        {
          platform: "jira",
          secretConfigured: false,
          lastReceivedUtc: null,
          acceptedUnsignedDelivery: false,
        },
      ],
    });

    render(<ConnectionsView />);

    expect(await screen.findByTestId("webhook-unsigned-github")).toHaveTextContent(
      "accepted an unsigned delivery",
    );
    expect(screen.queryByTestId("webhook-unsigned-jira")).not.toBeInTheDocument();
  });
});
