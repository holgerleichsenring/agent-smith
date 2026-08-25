"use client";

import { Component, type ErrorInfo, type ReactNode } from "react";
import { FailedSurface } from "./FailedSurface";

// 2026-08-25-39ab: the app had NO error boundary anywhere — no route one, no
// global one, no component that catches. One throw during render therefore
// unmounted the whole tree and left a blank document, which is what an operator
// saw when a payload carried a field this build did not expect.
//
// React offers no hook form of this: catching a render error is a class-only
// capability (getDerivedStateFromError / componentDidCatch), so this is the one
// class component in the app. It holds no logic beyond the catch — what a
// failure LOOKS like belongs to FailedSurface.
//
// Next's file-convention boundaries (error.tsx / global-error.tsx) cover a route
// and the root layout. This one covers a SURFACE inside a route, which is what
// keeps the rest of the page rendering when one card cannot.

interface Props {
  /** The surface this boundary guards, named as an operator would name it. */
  surface: string;
  children: ReactNode;
}

interface State {
  error: Error | null;
}

export class RenderBoundary extends Component<Props, State> {
  override state: State = { error: null };

  static getDerivedStateFromError(thrown: unknown): State {
    return { error: thrown instanceof Error ? thrown : new Error(String(thrown)) };
  }

  override componentDidCatch(thrown: Error, info: ErrorInfo): void {
    // The console is the browser's log. Swallowing this would hide where the
    // failure originated, which is the whole reason the blank page was a mystery.
    console.error(`[render] the ${this.props.surface} surface threw`, thrown, info.componentStack);
  }

  private readonly retry = () => this.setState({ error: null });

  override render(): ReactNode {
    if (this.state.error === null) return this.props.children;
    return (
      <FailedSurface surface={this.props.surface} error={this.state.error} onRetry={this.retry} />
    );
  }
}
