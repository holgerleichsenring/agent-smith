import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

// 2026-11-18-3863f: where a deploy stands after its run ended.

vi.mock("@/lib/deploymentsApi", () => ({ getDeployRecord: vi.fn() }));

const { getDeployRecord } = await import("@/lib/deploymentsApi");
const { DeployFollowBlock } = await import("../DeployFollowBlock");

const record = {
  runId: "r-1", project: "shop", context: "api", environment: "test", version: "v2", delivery: "gitops_pr", repo: "gitops",
  url: "https://host.example/gitops/pull/4", pullRequestState: "open", ciState: "red",
  failures: [{ name: "kubeconform", url: "https://ci.example/checks/9", excerpt: "deployment.yaml: invalid replicas" }],
  deliveredAt: "2026-11-18T10:00:00Z", checkedAt: "2026-11-18T10:05:00Z", settledAt: null, note: null,
};

beforeEach(() => vi.mocked(getDeployRecord).mockReset());

describe("DeployFollowBlock", () => {
  it("DeployFollowBlock_RedCheck_ShowsTheFailedCheckItsExcerptAndLink", async () => {
    vi.mocked(getDeployRecord).mockResolvedValue(record);
    render(<DeployFollowBlock runId="r-1" />);
    const failure = await screen.findByTestId("deploy-follow-failure");
    expect(failure.textContent).toContain("kubeconform");
    expect(failure.textContent).toContain("invalid replicas");
    expect(failure.querySelector("a")?.getAttribute("href")).toBe("https://ci.example/checks/9");
    expect(screen.getByTestId("deploy-follow-standing").textContent).toBe("open · CI red · followed");
  });

  it("DeployFollowBlock_RunDeliveredNothing_ShowsNothing", async () => {
    vi.mocked(getDeployRecord).mockResolvedValue(null);
    render(<DeployFollowBlock runId="r-2" />);
    await waitFor(() => expect(getDeployRecord).toHaveBeenCalled());
    expect(screen.queryByTestId("deploy-follow")).toBeNull();
  });
});
