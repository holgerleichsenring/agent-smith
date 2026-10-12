import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import type { ImageTrustBoundary, SandboxSetting } from "@/lib/configApi";
import {
  SandboxImageTrustFields,
  libraryChoice,
  librarySetting,
  withAddedRegistry,
} from "../SandboxImageTrustFields";
import { ImageTrustInForce } from "../ImageTrustInForce";

// 2026-10-12-6bfab: the trusted-registries section of the Sandbox settings and the boundary
// the server says is in force.

const NAMED: ImageTrustBoundary = {
  registries: ["docker.io/sbtscala/"],
  registriesAreDefault: false,
  dockerHubLibrary: false,
  dockerHubLibraryIsExplicit: false,
  defaultRegistries: ["mcr.microsoft.com/", "ghcr.io/"],
  description: "docker.io/sbtscala/",
};

const fetchImageTrust = vi.fn<(signal?: AbortSignal) => Promise<ImageTrustBoundary>>(() => Promise.resolve(NAMED));

vi.mock("@/lib/configApi", () => ({
  fetchImageTrust: (signal?: AbortSignal) => fetchImageTrust(signal),
}));

const SANDBOX: SandboxSetting = {
  agentRegistry: "ghcr.io/x",
  agentVersion: "",
  stepTimeoutSeconds: 900,
  runCommandTimeoutSeconds: 300,
  maxConcurrentSandboxes: null,
  holdSeconds: null,
  allowedRegistries: ["sbtscala/"],
  allowDockerHubLibrary: null,
};

beforeEach(() => vi.clearAllMocks());

describe("SandboxImageTrustFields", () => {
  it("LibrarySelect_MapsNullTrueFalse_BothWays", () => {
    expect(libraryChoice(null)).toBe("follow");
    expect(libraryChoice(undefined)).toBe("follow");
    expect(libraryChoice(true)).toBe("trust");
    expect(libraryChoice(false)).toBe("refuse");
    expect(librarySetting("follow")).toBeNull();
    expect(librarySetting("trust")).toBe(true);
    expect(librarySetting("refuse")).toBe(false);
  });

  it("WithAddedRegistry_FirstEntrySeedsTheDefault_LaterEntriesAppend", () => {
    expect(withAddedRegistry([], ["mcr.microsoft.com/", "ghcr.io/"])).toEqual(["mcr.microsoft.com/", "ghcr.io/", ""]);
    expect(withAddedRegistry(["a.example/"], ["mcr.microsoft.com/"])).toEqual(["a.example/", ""]);
  });

  it("Fields_EditRemoveAndSelect_EmitTheDraftPatch", async () => {
    const onChange = vi.fn();
    render(<SandboxImageTrustFields value={SANDBOX} onChange={onChange} persisted={SANDBOX} />);
    await screen.findByTestId("setting-sandbox-trust-inforce");

    fireEvent.change(screen.getByTestId("setting-sandbox-trust-0"), { target: { value: "docker.io/sbtscala/" } });
    expect(onChange).toHaveBeenLastCalledWith({ allowedRegistries: ["docker.io/sbtscala/"] });

    fireEvent.click(screen.getByTestId("setting-sandbox-trust-0-remove"));
    expect(onChange).toHaveBeenLastCalledWith({ allowedRegistries: [] });

    fireEvent.change(screen.getByTestId("setting-sandbox-trust-library"), { target: { value: "trust" } });
    expect(onChange).toHaveBeenLastCalledWith({ allowDockerHubLibrary: true });
    expect(screen.queryByTestId("setting-sandbox-trust-unsaved")).toBeNull();
  });

  it("Fields_AnOlderReadWithoutTheList_RendersEmpty", async () => {
    const legacy = { ...SANDBOX, allowedRegistries: undefined, allowDockerHubLibrary: undefined };
    render(<SandboxImageTrustFields value={legacy} onChange={vi.fn()} persisted={legacy} />);
    expect(await screen.findByTestId("setting-sandbox-trust-empty")).toHaveTextContent("ghcr.io/");
  });

  it("Fields_PersistedValueChanges_RefetchTheBoundaryInForce", async () => {
    const { rerender } = render(
      <SandboxImageTrustFields value={SANDBOX} onChange={vi.fn()} persisted={SANDBOX} />,
    );
    await waitFor(() => expect(fetchImageTrust).toHaveBeenCalledTimes(1));

    const saved = { ...SANDBOX, allowedRegistries: ["docker.io/sbtscala/"] };
    rerender(<SandboxImageTrustFields value={saved} onChange={vi.fn()} persisted={saved} />);

    await waitFor(() => expect(fetchImageTrust).toHaveBeenCalledTimes(2));
  });
});

describe("ImageTrustInForce", () => {
  it("NamedList_SaysItReplacesTheDefault_AndTheLibraryFollowsTheList", () => {
    render(<ImageTrustInForce boundary={NAMED} error={null} dirty />);
    expect(screen.getByTestId("setting-sandbox-trust-inforce")).toHaveTextContent("docker.io/sbtscala/");
    expect(screen.getByTestId("setting-sandbox-trust-inforce-registries")).toHaveTextContent(/replacing the built-in default/);
    expect(screen.getByTestId("setting-sandbox-trust-inforce-library")).toHaveTextContent(/refused — following the registry list/);
    expect(screen.getByTestId("setting-sandbox-trust-unsaved")).toBeInTheDocument();
  });

  it("ExplicitSwitch_SaysSo", () => {
    render(<ImageTrustInForce boundary={{ ...NAMED, dockerHubLibrary: true, dockerHubLibraryIsExplicit: true }}
      error={null} dirty={false} />);
    expect(screen.getByTestId("setting-sandbox-trust-inforce-library")).toHaveTextContent(/trusted — set explicitly/);
  });

  it("ReadFailed_SaysSo", () => {
    render(<ImageTrustInForce boundary={null} error={new Error("403")} dirty={false} />);
    expect(screen.getByTestId("setting-sandbox-trust-inforce-error")).toHaveTextContent("403");
  });
});
