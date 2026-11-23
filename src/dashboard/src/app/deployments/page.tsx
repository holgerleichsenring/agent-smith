import { Suspense } from "react";
import { DeploymentsView } from "@/components/deployments/DeploymentsView";

// 2026-11-23-8df6a: the Deployments page below Pull requests. ?days= is read under this Suspense boundary, which the
// static build requires of a search-param hook.
export default function DeploymentsPage() {
  return (
    <Suspense fallback={null}>
      <DeploymentsView />
    </Suspense>
  );
}
