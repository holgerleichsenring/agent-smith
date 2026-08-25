import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { RenderBoundary } from "../RenderBoundary";

// 2026-08-25-39ab: before this the app had no boundary of any kind, so a single
// throw during render unmounted the tree and left a blank document. These cases
// pin the two properties an operator depends on: the failure is NAMED, and it
// is CONTAINED.

function Throws({ message }: { message: string }): never {
  throw new Error(message);
}

// React logs a caught render error to console.error; the boundary logs its own
// line too. Neither is the subject under test, so they are silenced here.
let consoleError: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
});

afterEach(() => {
  consoleError.mockRestore();
});

describe("RenderBoundary", () => {
  it("Render_AComponentThrows_TheBoundaryShowsANamedErrorAndTheShellSurvives", () => {
    render(
      <div>
        <h1>agent-smith</h1>
        <RenderBoundary surface="run side rail">
          <Throws message="Cannot read properties of undefined (reading 'toLowerCase')" />
        </RenderBoundary>
      </div>,
    );

    const panel = screen.getByTestId("failed-surface");
    expect(panel).toHaveTextContent("The run side rail could not be rendered.");
    expect(screen.getByTestId("failed-surface-message")).toHaveTextContent("toLowerCase");
    // The shell around the failed surface is still on the page — not a blank document.
    expect(screen.getByText("agent-smith")).toBeInTheDocument();
  });

  it("Render_AComponentThrows_TheOtherSurfacesStillRender", () => {
    render(
      <div>
        <RenderBoundary surface="navigation rail">
          <Throws message="a payload this build does not know" />
        </RenderBoundary>
        <RenderBoundary surface="run monitor">
          <p>run 7f3c is running</p>
        </RenderBoundary>
      </div>,
    );

    expect(screen.getByTestId("failed-surface")).toHaveAttribute(
      "data-surface",
      "navigation rail",
    );
    expect(screen.getByText("run 7f3c is running")).toBeInTheDocument();
    // Exactly one surface failed — the sibling boundary never engaged.
    expect(screen.getAllByTestId("failed-surface")).toHaveLength(1);
  });

  it("RenderBoundary_NothingThrows_RendersItsChildrenUntouched", () => {
    render(
      <RenderBoundary surface="run monitor">
        <p>nothing wrong here</p>
      </RenderBoundary>,
    );

    expect(screen.getByText("nothing wrong here")).toBeInTheDocument();
    expect(screen.queryByTestId("failed-surface")).not.toBeInTheDocument();
  });

  it("RenderBoundary_TheOperatorRetries_RendersTheSurfaceAgain", () => {
    let shouldThrow = true;
    function Flaky() {
      if (shouldThrow) throw new Error("transient payload");
      return <p>recovered</p>;
    }

    render(
      <RenderBoundary surface="run view">
        <Flaky />
      </RenderBoundary>,
    );
    expect(screen.getByTestId("failed-surface")).toBeInTheDocument();

    shouldThrow = false;
    fireEvent.click(screen.getByTestId("failed-surface-retry"));

    expect(screen.getByText("recovered")).toBeInTheDocument();
    expect(screen.queryByTestId("failed-surface")).not.toBeInTheDocument();
  });

  it("RenderBoundary_SomethingThatIsNotAnError_StillNamesTheSurface", () => {
    function ThrowsAString(): never {
      // eslint-disable-next-line no-throw-literal
      throw "the server said no";
    }

    render(
      <RenderBoundary surface="configuration">
        <ThrowsAString />
      </RenderBoundary>,
    );

    expect(screen.getByTestId("failed-surface")).toHaveTextContent(
      "The configuration could not be rendered.",
    );
    expect(screen.getByTestId("failed-surface-message")).toHaveTextContent("the server said no");
  });
});
