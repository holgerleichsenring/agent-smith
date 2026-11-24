import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DraftCheckPanel } from "../DraftCheckPanel";

// 2026-10-02-5f89b: the Test action renders the server's steps, each with its mark, ending at
// the failing one — and sends the unsaved draft as it stands.

const checkConnectionDraft = vi.fn();
const checkTrackerDraft = vi.fn();
const checkPocArgoDraft = vi.fn();

vi.mock("@/lib/configApi", () => ({
  checkConnectionDraft: (...args: unknown[]) => checkConnectionDraft(...args),
  checkTrackerDraft: (...args: unknown[]) => checkTrackerDraft(...args),
  checkPocArgoDraft: (...args: unknown[]) => checkPocArgoDraft(...args),
}));

const draft = { id: "gl", type: "gitlab", authSecret: "gitlab_a", organization: "acme", host: "https://git.example.test" };

describe("DraftCheckPanel", () => {
  beforeEach(() => {
    checkConnectionDraft.mockReset();
    checkTrackerDraft.mockReset();
    checkPocArgoDraft.mockReset();
  });

  it("the connection form's Test renders each step with its mark and stops at the failing one", async () => {
    checkConnectionDraft.mockResolvedValue({
      ok: false,
      steps: [
        { key: "secret", label: "Secret", ok: true, detail: "Secret 'gitlab_a' has a value." },
        { key: "host", label: "Host", ok: true, detail: "git.example.test answered HTTP 401." },
        { key: "identity", label: "Identity", ok: false, detail: "HTTP 401: the token was not accepted." },
      ],
    });
    render(<DraftCheckPanel kind="connections" draft={draft} />);

    fireEvent.click(screen.getByTestId("draft-check-run"));

    await waitFor(() => expect(screen.getByTestId("draft-check-steps")).toBeInTheDocument());
    expect(checkConnectionDraft.mock.calls[0][0]).toEqual(draft);
    const steps = screen.getByTestId("draft-check-steps").querySelectorAll("li");
    expect(steps).toHaveLength(3);
    expect(screen.getByTestId("draft-check-step-secret").dataset.ok).toBe("true");
    expect(screen.getByTestId("draft-check-step-identity").dataset.ok).toBe("false");
    expect(screen.getByTestId("draft-check-step-identity")).toHaveTextContent("✗ Identity — HTTP 401");
    expect(steps[steps.length - 1]).toBe(screen.getByTestId("draft-check-step-identity"));
  });

  it("a tracker draft goes to the tracker check, and a refusal shows the request's error", async () => {
    checkTrackerDraft.mockRejectedValue(new Error("HTTP 403: missing diagnostics.probe"));
    render(<DraftCheckPanel kind="trackers" draft={{ id: "t", type: "jira", authSecret: "jira_a" }} />);

    fireEvent.click(screen.getByTestId("draft-check-run"));

    await waitFor(() => expect(screen.getByTestId("draft-check-error")).toHaveTextContent("missing diagnostics.probe"));
    expect(checkConnectionDraft).not.toHaveBeenCalled();
  });

  // 2026-11-23-c81ec: a target's Argo block is checked under the target's name, with its own label and test ids.
  it("a poc-argo draft goes to the Argo check with the target's name and renders its steps", async () => {
    checkPocArgoDraft.mockResolvedValue({
      ok: true, steps: [{ key: "token", label: "Token", ok: true, detail: "The token has no expiry." }],
    });
    const argo = {
      url: "https://argocd.example.internal", tokenSecret: "argo_role_token", project: "pocs", namespace: "argocd",
      destinationServer: "https://kubernetes.default.svc", repoUrl: "https://git.example.internal/poc-source.git",
      applicationSet: null, tokenWarnDays: 14, generatorRequeueSeconds: 30,
    };
    render(<DraftCheckPanel kind="poc-argo" target="main" draft={argo} label="Test connection" help="checks it" testId="argo-check" />);

    fireEvent.click(screen.getByTestId("argo-check-run"));

    await waitFor(() => expect(screen.getByTestId("argo-check-step-token")).toHaveTextContent("no expiry"));
    expect(checkPocArgoDraft.mock.calls[0][0]).toBe("main");
    expect(checkPocArgoDraft.mock.calls[0][1]).toEqual(argo);
    expect(checkConnectionDraft).not.toHaveBeenCalled();
  });
});
