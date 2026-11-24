import { useState } from "react";
import { fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { PocArgoSetting, StudioEntity } from "@/lib/configApi";
import { newPocArgo, PocArgoFields } from "../PocArgoFields";

// 2026-11-23-c81ec: a target's Argo CD section edits the argo block, and Test connection posts {target, argo} with the
// values in the form and shows the server's steps — the token's expiry and the ApplicationSet among them.

vi.mock("@/lib/auth/session", () => ({ currentAccessToken: async () => null }));

const SECRETS = [{ id: "argo_role_token" }] as StudioEntity[];

function Harness({ seen }: { seen: PocArgoSetting[] }) {
  const [argo, setArgo] = useState<PocArgoSetting | null>(null);
  return (
    <PocArgoFields argo={argo} target="main" secrets={SECRETS} testId="setting-pocs-target-0"
      onChange={(next) => {
        seen.push(next);
        setArgo(next);
      }} />
  );
}

afterEach(() => vi.unstubAllGlobals());

describe("PocArgoFields", () => {
  it("PocArgoFields_RendersTheDefaults_AndEditsRoundTrip", () => {
    const seen: PocArgoSetting[] = [];
    render(<Harness seen={seen} />);
    const id = "setting-pocs-target-0-argo";
    fireEvent.click(screen.getByTestId(`${id}-toggle`));

    expect(screen.getByTestId(`${id}-namespace`)).toHaveValue("argocd");
    expect(screen.getByTestId(`${id}-destination-server`)).toHaveValue("https://kubernetes.default.svc");
    expect(screen.getByTestId(`${id}-token-warn-days`)).toHaveValue(14);
    expect(screen.getByTestId(`${id}-generator-requeue-seconds`)).toHaveValue(30);
    expect(screen.getByTestId(`${id}-application-set`)).toHaveAttribute("placeholder", "pocs-main");

    fireEvent.change(screen.getByTestId(`${id}-url`), { target: { value: "https://argocd.example.internal" } });
    fireEvent.change(screen.getByTestId(`${id}-token-secret`), { target: { value: "argo_role_token" } });
    fireEvent.change(screen.getByTestId(`${id}-project`), { target: { value: "pocs" } });
    fireEvent.change(screen.getByTestId(`${id}-repo-url`), { target: { value: "https://git.example.internal/platform/poc-source.git" } });
    fireEvent.change(screen.getByTestId(`${id}-application-set`), { target: { value: "pocs-custom" } });
    fireEvent.change(screen.getByTestId(`${id}-generator-requeue-seconds`), { target: { value: "20" } });

    expect(seen[seen.length - 1]).toEqual({
      ...newPocArgo(), url: "https://argocd.example.internal", tokenSecret: "argo_role_token", project: "pocs",
      repoUrl: "https://git.example.internal/platform/poc-source.git", applicationSet: "pocs-custom", generatorRequeueSeconds: 20,
    });

    fireEvent.change(screen.getByTestId(`${id}-application-set`), { target: { value: "" } });
    expect(seen[seen.length - 1].applicationSet).toBeNull();
  });

  it("PocArgoFields_TestConnection_PostsTheTargetAndTheArgoBlock_AndShowsTheSteps", async () => {
    const report = {
      ok: true,
      steps: [
        { key: "token", label: "Token", ok: true, detail: "The token expires on 2027-02-20." },
        { key: "application_set", label: "ApplicationSet", ok: false, detail: "pocs-main is absent; run Install." },
      ],
    };
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true, status: 200, text: async () => JSON.stringify(report), json: async () => report,
      headers: { get: () => "application/json" },
    } as unknown as Response);
    vi.stubGlobal("fetch", fetchMock);
    render(<Harness seen={[]} />);
    const id = "setting-pocs-target-0-argo";
    fireEvent.click(screen.getByTestId(`${id}-toggle`));
    fireEvent.change(screen.getByTestId(`${id}-url`), { target: { value: "https://argocd.example.internal" } });

    fireEvent.click(screen.getByTestId(`${id}-check-run`));

    expect(await screen.findByTestId(`${id}-check-step-token`)).toHaveTextContent("The token expires on 2027-02-20.");
    expect(screen.getByTestId(`${id}-check-step-application_set`)).toHaveTextContent("run Install");
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(String(url)).toMatch(/\/api\/config\/pocs\/argo\/check$/);
    expect(init.method).toBe("POST");
    expect(JSON.parse(String(init.body))).toEqual({
      target: "main", argo: { ...newPocArgo(), url: "https://argocd.example.internal" },
    });
  });
});
