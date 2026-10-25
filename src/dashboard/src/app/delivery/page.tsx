import { DeliveryView } from "@/components/delivery/DeliveryView";

// 2026-10-25-0099c: the Delivery page under Insight. The body lives in the view so the page exports only its
// default, satisfying Next's Page-type contract.
export default function DeliveryPage() {
  return <DeliveryView />;
}
