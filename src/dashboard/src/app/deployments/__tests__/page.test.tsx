import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { ApiRefusal } from "@/lib/apiResponse";
import type { DeploymentsPageView, DeployRecordView, MergedPullRequestItem } from "@/lib/deploymentsApi";

// 2026-11-23-8df6a: the Deployments page — merged rows with the short merge commit or 'merge commit not read', the
// deploy cards, the time span in the address, and its empty and refused states.

const hoisted = vi.hoisted(() => ({ search: "", replace: vi.fn(), fetch: vi.fn(), reach: "unknown" as "none" | "unknown" }));
vi.mock("next/navigation", () => ({
  usePathname: () => "/deployments",
  useSearchParams: () => new URLSearchParams(hoisted.search),
  useRouter: () => ({ replace: hoisted.replace, push: vi.fn() }),
}));
vi.mock("@/lib/deploymentsApi", () => ({ fetchDeploymentsPage: (...a: unknown[]) => hoisted.fetch(...a) }));
vi.mock("@/hooks/useProjectReach", () => ({ useProjectReach: () => hoisted.reach }));
vi.mock("@/lib/auth/session", () => ({ signIn: vi.fn() }));

import DeploymentsPage from "../page";

function merged(over: Partial<MergedPullRequestItem> = {}): MergedPullRequestItem {
  return {
    project: "shop", ticketId: "42", ticketTitle: "Faster checkout", repo: "api", number: "7", url: "https://git/pr/7",
    targetBranch: "main", mergeSha: "0123456789abcdef", mergedAt: new Date().toISOString(), runId: "r-1", ...over,
  };
}

function deploy(): DeployRecordView {
  return {
    runId: "d-1", project: "shop", context: "api", environment: "test", version: "v2", delivery: "pipeline_trigger", repo: "app",
    url: "https://ci/runs/1", pullRequestState: null, ciState: "green", failures: [], deliveredAt: new Date().toISOString(),
    checkedAt: null, settledAt: new Date().toISOString(), note: null,
  };
}

function answer(view: DeploymentsPageView) {
  hoisted.fetch.mockResolvedValue(view);
}

beforeEach(() => {
  hoisted.search = "";
  hoisted.reach = "unknown";
  hoisted.replace.mockReset();
  hoisted.fetch.mockReset();
});

describe("DeploymentsPage", () => {
  it("DeploymentsPage_MergedRows_ShortShaOrNotRead_AndDeployCards", async () => {
    answer({ merged: [merged(), merged({ runId: "r-2", number: "8", url: "https://git/pr/8", mergeSha: null })], deploys: [deploy()] });
    render(<DeploymentsPage />);

    expect(await screen.findByTestId("deployments-merged-r-1-7-sha")).toHaveTextContent("0123456");
    expect(screen.getByTestId("deployments-merged-r-2-8-sha")).toHaveTextContent("merge commit not read");
    expect(screen.getByTestId("deployments-merged-r-1-7-ticket")).toHaveAttribute("href", "/tickets/shop/42");
    expect(screen.getByTestId("deployments-merged-r-1-7-link")).toHaveAttribute("href", "https://git/pr/7");
    expect(screen.getByTestId("deployments-merged-r-1-7")).toHaveTextContent("into main");
    expect(screen.getByTestId("deployments-deploy-d-1")).toHaveTextContent("api v2 to test");
  });

  it("DeploymentsPage_DaysInTheAddress_ReadsThatSpan_AndAChoiceWritesIt", async () => {
    hoisted.search = "days=30";
    answer({ merged: [merged()], deploys: [] });
    render(<DeploymentsPage />);

    await waitFor(() => expect(hoisted.fetch).toHaveBeenCalledWith(30, expect.anything()));
    expect(screen.getByTestId("deployments-days-30")).toHaveAttribute("aria-pressed", "true");
    fireEvent.click(screen.getByTestId("deployments-days-90"));
    expect(hoisted.replace).toHaveBeenCalledWith("/deployments?days=90", { scroll: false });
    fireEvent.click(screen.getByTestId("deployments-days-7"));
    expect(hoisted.replace).toHaveBeenCalledWith("/deployments", { scroll: false });
  });

  it("DeploymentsPage_UnknownDays_ReadsSevenDays", async () => {
    hoisted.search = "days=12";
    answer({ merged: [], deploys: [] });
    render(<DeploymentsPage />);

    await waitFor(() => expect(hoisted.fetch).toHaveBeenCalledWith(7, expect.anything()));
  });

  it("DeploymentsPage_Nothing_SaysSo_OrThatNoProjectIsReached", async () => {
    answer({ merged: [], deploys: [] });
    const first = render(<DeploymentsPage />);
    expect(await screen.findByTestId("deployments-empty")).toHaveTextContent("Nothing merged or deployed");
    first.unmount();

    hoisted.reach = "none";
    render(<DeploymentsPage />);
    expect(await screen.findByTestId("no-project-reach")).toBeInTheDocument();
  });

  it("DeploymentsPage_Refused_ShowsTheRefusal", async () => {
    hoisted.fetch.mockRejectedValue(new ApiRefusal("/api/deployments", 403, "permission", ["runs.read"]));
    render(<DeploymentsPage />);

    expect(await screen.findByTestId("refusal-surface")).toBeInTheDocument();
  });
});
