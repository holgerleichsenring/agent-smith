import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

// 2026-11-18-3863d: Deploy from a ticket's page.

const hoisted = vi.hoisted(() => ({ lacks: false }));
vi.mock("@/hooks/useLacksPermission", () => ({ useLacksPermission: () => hoisted.lacks }));
vi.mock("@/lib/deploymentsApi", () => ({ listDeployments: vi.fn(), startDeploy: vi.fn() }));

const { listDeployments, startDeploy } = await import("@/lib/deploymentsApi");
const { DeployAct } = await import("../DeployAct");

const binding = {
  context: "api", contextRepo: null, template: "delivery · k8s-gitops @ v1",
  environments: [{ name: "test", approval: false, repo: null }, { name: "prod", approval: true, repo: "gitops" }],
};

beforeEach(() => {
  hoisted.lacks = false;
  vi.mocked(listDeployments).mockReset().mockResolvedValue([binding]);
  vi.mocked(startDeploy).mockReset();
});

describe("DeployAct", () => {
  it("DeployAct_WithoutRunsControl_IsHidden", () => {
    hoisted.lacks = true;
    render(<DeployAct project="shop" ticketId="7" onStarted={() => {}} />);
    expect(screen.queryByTestId("deploy-act")).toBeNull();
    expect(listDeployments).not.toHaveBeenCalled();
  });

  it("DeployAct_NoDeploymentBinding_IsHidden", async () => {
    vi.mocked(listDeployments).mockResolvedValue([]);
    render(<DeployAct project="shop" ticketId="7" onStarted={() => {}} />);
    await waitFor(() => expect(listDeployments).toHaveBeenCalled());
    expect(screen.queryByTestId("deploy-act")).toBeNull();
  });

  it("DeployAct_StartsWithTheChosenEnvironmentAndVersion", async () => {
    vi.mocked(startDeploy).mockResolvedValue({ kind: "started", runId: "r-1" });
    const started = vi.fn();
    render(<DeployAct project="shop" ticketId="7" onStarted={started} />);
    fireEvent.click(await screen.findByTestId("deploy-open"));
    fireEvent.change(screen.getByTestId("deploy-environment"), { target: { value: "prod" } });
    expect(screen.getByTestId("deploy-approval")).toHaveTextContent("a person merges");
    fireEvent.change(screen.getByTestId("deploy-version"), { target: { value: "" } });
    fireEvent.click(screen.getByTestId("deploy-start"));

    await screen.findByTestId("deploy-started");
    expect(startDeploy).toHaveBeenCalledWith("shop",
      { context: "api", contextRepo: null, environment: "prod", version: null, ticketId: "7" });
    expect(started).toHaveBeenCalled();
  });

  it("DeployAct_ServerRefusal_IsShownAsWritten", async () => {
    vi.mocked(startDeploy).mockResolvedValue({ kind: "refused", text: "version 'sha256:x' is not deliverable" });
    render(<DeployAct project="shop" ticketId="7" onStarted={() => {}} />);
    fireEvent.click(await screen.findByTestId("deploy-open"));
    fireEvent.change(screen.getByTestId("deploy-version"), { target: { value: "sha256:x" } });
    fireEvent.click(screen.getByTestId("deploy-start"));

    expect(await screen.findByTestId("deploy-refused")).toHaveTextContent("is not deliverable");
  });
});
