import type { Metadata } from "next";
import { IBM_Plex_Mono, Inter } from "next/font/google";
import { AppSidebar } from "@/components/shell/AppSidebar";
import { AppTopBar } from "@/components/shell/AppTopBar";
import { RenderBoundary } from "@/components/shell/RenderBoundary";
import { DegradedBanner } from "@/components/shell/DegradedBanner";
import { BuildMismatchBanner } from "@/components/shell/BuildMismatchBanner";
import { AuthMisconfigurationBanner } from "@/components/shell/AuthMisconfigurationBanner";
import { SignInGate } from "@/components/shell/SignInGate";
import { ConfigCatalogProvider } from "@/components/config/ConfigCatalogProvider";
import { EventStoreProvider } from "@/lib/eventStore/EventStoreProvider";
import { RunBucketFilterProvider } from "@/lib/RunBucketFilter";
import { TicketBucketFilterProvider } from "@/lib/TicketBucketFilter";
import { TicketListProvider } from "@/lib/tickets/TicketListProvider";
import { RuntimeSettingsProvider } from "@/lib/runtimeSettings/RuntimeSettingsProvider";
import "./globals.css";

// p0174: Inter is the DESIGN.md primary typography — load via next/font
// so it's self-hosted, font-display:swap by default, and bound to a CSS
// variable the rest of the app consumes via Tailwind's font-sans utility.
const inter = Inter({
  subsets: ["latin"],
  variable: "--font-sans",
  display: "swap",
});
// 2026-11-16-0440c: IBM Plex Mono for ids, branches and code, self-hosted the same way.
const plexMono = IBM_Plex_Mono({
  subsets: ["latin"],
  weight: ["400", "500"],
  variable: "--font-plex-mono",
  display: "swap",
});

export const metadata: Metadata = {
  title: "agent-smith",
  description: "AI orchestration framework — self-hosted",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${inter.variable} ${plexMono.variable}`}>
      <body className="bg-[var(--color-canvas)] text-[var(--color-ink)] font-sans">
        {/* p0218: the shared EventStore lives above every route so the system
            backlog survives navigation and one subscription feeds all views. */}
        {/* p0353: the config catalog is a SINGLE shared instance above the config
            section list and the config page, so an import/save/revert's reload()
            refreshes the counts and the studio pane in one shot. */}
        {/* p0458: the run bucket and the ticket bucket are shared by the sidebar and
            the lists' state pills, so the marked item and the shown list agree. */}
        {/* 2026-08-25-21ae: the settings the container was started with, read
            once above every route. Two reads would be two answers. */}
        <RuntimeSettingsProvider>
          <EventStoreProvider>
            <ConfigCatalogProvider>
              <RunBucketFilterProvider>
                <TicketBucketFilterProvider>
                <TicketListProvider>
                {/* 2026-08-25-39ab: the layout sits ABOVE the route boundary, so a
                  throw in the sidebar or a banner escapes error.tsx and blanks the
                  document. Each gets its own boundary. */}
                {/* 2026-11-16-0440c: the flat sidebar runs the full height on the left;
                  the top bar and the scrolling main share the column beside it. */}
                <div className="app-shell">
                  <RenderBoundary surface="navigation sidebar">
                    <AppSidebar />
                  </RenderBoundary>
                  <div className="app-column">
                    <RenderBoundary surface="top bar">
                      <AppTopBar />
                    </RenderBoundary>
                    {/* p0391a: the server always starts, so "it came up" no longer means
                      "it is fine" — the banner names what is down, above every route. */}
                    <main className="shell-main">
                      <RenderBoundary surface="installation health banner">
                        <DegradedBanner />
                      </RenderBoundary>
                      {/* 2026-08-25-8c97: the same findings document, read for the one
                        finding whose remedy is a reload rather than an operator. */}
                      <RenderBoundary surface="build identity banner">
                        <BuildMismatchBanner />
                      </RenderBoundary>
                      {/* 2026-08-25-4530: the two halves of sign-in live on two
                        machines and neither can diagnose the other. This is the
                        only place that holds both, and a half-configured
                        installation otherwise renders nothing at all. */}
                      <RenderBoundary surface="sign-in configuration banner">
                        <AuthMisconfigurationBanner />
                      </RenderBoundary>
                      {/* 2026-09-21-291b: the routes, and only the routes. The profile
                        keeps its own sign-in and the banners keep explaining a
                        half-configured installation — a gate that took those with it
                        would remove the two surfaces that say WHY nothing is visible. */}
                      <RenderBoundary surface="sign-in gate">
                        <SignInGate>{children}</SignInGate>
                      </RenderBoundary>
                    </main>
                  </div>
                </div>
                </TicketListProvider>
                </TicketBucketFilterProvider>
              </RunBucketFilterProvider>
            </ConfigCatalogProvider>
          </EventStoreProvider>
        </RuntimeSettingsProvider>
      </body>
    </html>
  );
}
