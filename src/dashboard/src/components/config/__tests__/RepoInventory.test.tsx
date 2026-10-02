import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { RepoInventory } from "../RepoInventory";

// 2026-10-02-5f89c: a connection's section shows its last success and, beside it, the last
// failed attempt with its reason; Refresh now asks the server and re-reads what it now answers.

const fetchConnectionRepos = vi.fn();
const refreshConnectionDiscovery = vi.fn();

vi.mock("@/lib/configApi", () => ({
  fetchConnectionRepos: (...args: unknown[]) => fetchConnectionRepos(...args),
  refreshConnectionDiscovery: (...args: unknown[]) => refreshConnectionDiscovery(...args),
}));

const connection = { id: "conn", type: "gitlab", authSecret: "gitlab_a", organization: "acme" };

describe("RepoInventory discovery status", () => {
  beforeEach(() => {
    fetchConnectionRepos.mockReset();
    refreshConnectionDiscovery.mockReset();
  });

  it("RepoInventory shows the last error beside the last success and re-reads after Refresh now", async () => {
    fetchConnectionRepos
      .mockResolvedValueOnce({
        discoveredAt: "2026-10-01T08:00:00Z",
        repos: [{ name: "api", defaultBranch: "main" }],
        lastAttemptAt: "2026-10-02T08:00:00Z",
        lastError: "GitLab repo discovery for 'conn' failed: HTTP 401.",
        repoCount: 1,
      })
      .mockResolvedValueOnce({
        discoveredAt: "2026-10-02T09:00:00Z",
        repos: [{ name: "api", defaultBranch: "main" }, { name: "web", defaultBranch: null }],
        lastAttemptAt: "2026-10-02T09:00:00Z",
        lastError: null,
        repoCount: 2,
      });
    refreshConnectionDiscovery.mockResolvedValue({});
    render(<RepoInventory connections={[connection]} projects={[]} />);

    expect(await screen.findByTestId("repo-inventory-conn-success")).toHaveTextContent("· 1 repos");
    expect(screen.getByTestId("repo-inventory-conn-lasterror")).toHaveTextContent("failed: GitLab repo discovery for 'conn' failed: HTTP 401.");

    fireEvent.click(screen.getByTestId("repo-inventory-conn-refresh"));

    await waitFor(() => expect(screen.getByTestId("repo-inventory-conn-success")).toHaveTextContent("· 2 repos"));
    expect(refreshConnectionDiscovery).toHaveBeenCalledWith("conn");
    expect(fetchConnectionRepos).toHaveBeenCalledTimes(2);
    expect(screen.queryByTestId("repo-inventory-conn-lasterror")).not.toBeInTheDocument();
  });

  it("a refused Refresh now says why and keeps the last answer", async () => {
    fetchConnectionRepos.mockResolvedValue({ discoveredAt: null, repos: [], lastError: null });
    refreshConnectionDiscovery.mockRejectedValue(new Error("HTTP 403"));
    render(<RepoInventory connections={[connection]} projects={[]} />);

    expect(await screen.findByTestId("repo-inventory-undiscovered-conn")).toHaveTextContent("Refresh now");
    fireEvent.click(screen.getByTestId("repo-inventory-conn-refresh"));

    expect(await screen.findByTestId("repo-inventory-conn-refresh-error")).toHaveTextContent("HTTP 403");
    expect(screen.getByTestId("repo-inventory-conn-nosuccess")).toBeInTheDocument();
  });
});
