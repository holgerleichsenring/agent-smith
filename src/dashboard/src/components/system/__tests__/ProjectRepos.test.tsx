import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { ProjectRepos } from "../ProjectRepos";
import type { ConfigRepo } from "@/lib/configApi";
import type { RepoBootstrap } from "@/lib/projectBootstrapApi";

// 2026-10-13-0b82c: each repository row says whether its default branch is bootstrapped, from one
// read per project — so a person sees why a command on it is answered instead of run.

const api = vi.hoisted(() => ({
  fetchProjectBootstrap: vi.fn<(project: string) => Promise<RepoBootstrap[]>>(),
}));

vi.mock("@/lib/projectBootstrapApi", () => api);

const repo = (name: string): ConfigRepo => ({
  name, type: "AzureDevOps", url: `https://dev.azure.com/acme/p/_git/${name}`, organization: "acme", project: "p", defaultBranch: null,
});

describe("ProjectRepos shows each repository's bootstrap", () => {
  beforeEach(() => {
    api.fetchProjectBootstrap.mockReset();
  });

  it("ProjectRepos shows not initialised with missing lines, initialised, and the unknown reason — from one read", async () => {
    api.fetchProjectBootstrap.mockResolvedValue([
      { repo: "server", branch: "default", state: "incomplete", reason: null,
        missing: ["'api' has no principles.md and carries the retired coding-principles.md instead"] },
      { repo: "client", branch: "develop", state: "complete", missing: [], reason: null },
      { repo: "worker", branch: "default", state: "unknown", missing: [], reason: "401 Unauthorized" },
    ]);

    render(<ProjectRepos project="sample" repos={[repo("server"), repo("client"), repo("worker")]} />);

    expect((await screen.findByTestId("repo-bootstrap-server")).textContent).toBe("not initialised");
    expect(screen.getByTestId("repo-bootstrap-detail-server").textContent).toContain("'api' has no principles.md");
    expect(screen.getByTestId("repo-bootstrap-client").textContent).toBe("initialised");
    expect(screen.queryByTestId("repo-bootstrap-detail-client")).toBeNull();
    expect(screen.getByTestId("repo-bootstrap-detail-worker").textContent).toContain("401 Unauthorized");
    expect(api.fetchProjectBootstrap).toHaveBeenCalledTimes(1);
  });

  it("ProjectRepos shows the repositories without a state when the read is refused", async () => {
    api.fetchProjectBootstrap.mockRejectedValue(new Error("403"));

    render(<ProjectRepos project="sample" repos={[repo("server")]} />);

    expect(await screen.findByTestId("repo-server")).toBeTruthy();
    expect(screen.queryByTestId("repo-bootstrap-server")).toBeNull();
  });
});
