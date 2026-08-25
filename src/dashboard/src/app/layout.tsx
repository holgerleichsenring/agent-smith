import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { AppRail } from "@/components/shell/AppRail";
import { RenderBoundary } from "@/components/shell/RenderBoundary";
import { DegradedBanner } from "@/components/shell/DegradedBanner";
import { BuildMismatchBanner } from "@/components/shell/BuildMismatchBanner";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { RunBucketFilterProvider } from "@/lib/RunBucketFilter";
import "./globals.css";

// p0174: Inter is the DESIGN.md primary typography — load via next/font
// so it's self-hosted, font-display:swap by default, and bound to a CSS
// variable the rest of the app consumes via Tailwind's font-sans utility.
const inter = Inter({
  subsets: ["latin"],
  variable: "--font-sans",
  display: "swap",
});

export const metadata: Metadata = {
  title: "agent-smith",
  description: "AI orchestration framework — self-hosted",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={inter.variable}>
      <body className="bg-[var(--color-canvas)] text-[var(--color-ink)] font-sans">
        {/* p0209a: persistent left app rail + scrollable full-height main.
            [248px 1fr] grid replaces the topbar; every route renders inside. */}
        {/* p0218: the shared EventStore lives above every route so the system
            backlog survives navigation and one subscription feeds all views. */}
        {/* p0343c: the mock shell — 230px rail per the ratified mockups' .app grid. */}
        {/* p0353: the config catalog is a SINGLE shared instance above both the
            rail and the config page, so an import/save/revert's reload() refreshes
            the rail's count badges and the studio pane in one shot. */}
        {/* p0458: the monitor bucket the rail selects is shared by the rail and
            the run list below it, so the highlighted item and the visible
            sections are always the same answer. */}
        <EventStoreProvider>
          <ConfigCatalogProvider>
            <RunBucketFilterProvider>
              {/* 2026-08-25-39ab: the layout sits ABOVE the route boundary, so a
                  throw in the rail or the banner escapes error.tsx and blanks the
                  document. Each gets its own boundary: the rail failing must not
                  cost the operator the run they were reading. */}
              <div className="grid min-h-screen grid-cols-[230px_1fr]">
                <RenderBoundary surface="navigation rail">
                  <AppRail />
                </RenderBoundary>
                {/* p0391a: the server always starts, so "it came up" no longer means
                    "it is fine" — the banner names what is down, above every route. */}
                <main className="h-screen overflow-y-auto">
                  <RenderBoundary surface="installation health banner">
                    <DegradedBanner />
                  </RenderBoundary>
                  {/* 2026-08-25-8c97: the same findings document, read for the one
                      finding whose remedy is a reload rather than an operator. */}
                  <RenderBoundary surface="build identity banner">
                    <BuildMismatchBanner />
                  </RenderBoundary>
                  {children}
                </main>
              </div>
            </RunBucketFilterProvider>
          </ConfigCatalogProvider>
        </EventStoreProvider>
      </body>
    </html>
  );
}
