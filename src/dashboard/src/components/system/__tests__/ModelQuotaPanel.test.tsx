import { render, screen } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { ModelQuotaPanel } from "../ModelQuotaPanel";
import * as api from "@/lib/modelQuotaApi";

vi.mock("@/lib/modelQuotaApi", () => ({ fetchModelQuota: vi.fn() }));

const mocked = api as unknown as { fetchModelQuota: ReturnType<typeof vi.fn> };

const deployment: api.DeploymentQuota = {
  provider: "azure_openai",
  deployment: "gpt-terra",
  tokensPerMinute: 250_000,
  requestsPerMinute: 250,
  freeTokens: 180_000,
  freeRequests: 231,
  secondsToFull: 16.2,
  pausedSeconds: 3.4,
  waiting: 2,
  providerRemainingTokens: 175_000,
  providerRemainingRequests: 229,
  providerReadAt: "2026-11-22T10:00:00Z",
};

describe("ModelQuotaPanel", () => {
  beforeEach(() => {
    mocked.fetchModelQuota.mockReset();
  });

  it("ModelQuotaPanel_ADeployment_ShowsFreeTokensOfTheLimitAndWhenItIsFull", async () => {
    mocked.fetchModelQuota.mockResolvedValue({ replica: "server-a", deployments: [deployment] });

    render(<ModelQuotaPanel />);

    expect(await screen.findByText("gpt-terra: 180k of 250k tokens free")).toBeInTheDocument();
    const detail = screen.getByTestId("model-quota-detail-gpt-terra").textContent ?? "";
    expect(detail).toContain("231 of 250 requests free");
    expect(detail).toContain("full again in 17 s");
    expect(detail).toContain("paused 4 s more: the provider refused a call");
    expect(detail).toContain("2 calls waiting");
    expect(detail).toContain("the provider last said 175k tokens left");
    expect(screen.getByText(/on this server \(server-a\)/)).toBeInTheDocument();
  });

  it("ModelQuotaPanel_NoCallYet_SaysSo", async () => {
    mocked.fetchModelQuota.mockResolvedValue({ replica: "server-a", deployments: [] });

    render(<ModelQuotaPanel />);

    expect(await screen.findByTestId("model-quota-empty")).toBeInTheDocument();
  });

  it("ModelQuotaPanel_ReadFails_NamesTheReason", async () => {
    mocked.fetchModelQuota.mockRejectedValue(new Error("403"));

    render(<ModelQuotaPanel />);

    expect((await screen.findByTestId("model-quota-error")).textContent).toBe("Could not read the model quota: 403");
  });
});
