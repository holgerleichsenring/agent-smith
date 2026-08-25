import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { RefusalSurface } from "../RefusalSurface";
import { ApiRefusal } from "@/lib/apiResponse";

// 2026-08-25-4530: each refusal offers the ONE action that resolves it. A person
// who lacks a permission cannot fix it by signing in again, so a sign-in button
// there would be a loop with a promise in it — that absence is the assertion.
const session = vi.hoisted(() => ({ signIn: vi.fn() }));
vi.mock("@/lib/auth/session", () => ({ signIn: session.signIn }));

describe("RefusalSurface", () => {
  // Braces matter: a beforeEach that RETURNS the mock hands vitest the mock
  // itself as the teardown hook, which then calls it after the test.
  beforeEach(() => {
    session.signIn.mockReset();
  });

  it("Refusal_SignInState_OffersSignIn", () => {
    render(
      <RefusalSurface
        refusal={new ApiRefusal("/api/runs", 401, "sign-in", [])}
        surface="the run list"
      />,
    );

    expect(screen.getByTestId("refusal-surface")).toHaveTextContent("You are signed out");
    fireEvent.click(screen.getByTestId("refusal-sign-in"));
    expect(session.signIn).toHaveBeenCalledOnce();
  });

  it("Refusal_MissingPermission_NamesItAndOffersNoSignIn", () => {
    render(
      <RefusalSurface
        refusal={new ApiRefusal("/api/config/secrets", 403, "permission", ["secrets.read"])}
        surface="the secrets catalog"
      />,
    );

    expect(screen.getByTestId("refusal-missing-permissions")).toHaveTextContent("secrets.read");
    expect(screen.queryByTestId("refusal-sign-in")).toBeNull();
  });

  it("names the refusal even when the server named no permission", () => {
    render(<RefusalSurface refusal={new ApiRefusal("/api/config", 403, "permission", [])} />);

    expect(screen.getByTestId("refusal-unnamed")).toHaveTextContent("named no permission");
    expect(screen.queryByTestId("refusal-sign-in")).toBeNull();
  });
});
