The plan is cut into twenty tickets that merge one after another; each run derives its own specs when it starts.

```outcome
kind: tickets
tickets:
  - key: "orders-schema"
    title: "Orders table carries the fulfilment state"
    requirement: "Add the fulfilment state column so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the fulfilment state column changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The fulfilment state column works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The fulfilment state column is described in the changelog under the fulfilment heading"
    requires: []
    repos: ["api"]
  - key: "orders-migration"
    title: "Existing orders are migrated to the fulfilment state"
    requirement: "Add the order migration so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the order migration changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The order migration works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The order migration is described in the changelog under the fulfilment heading"
    requires: ["orders-schema"]
    repos: ["api"]
  - key: "orders-api-read"
    title: "GET /orders returns the fulfilment state"
    requirement: "Add the order read endpoint so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the order read endpoint changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The order read endpoint works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The order read endpoint is described in the changelog under the fulfilment heading"
    requires: ["orders-schema"]
    repos: ["api"]
  - key: "orders-api-write"
    title: "PATCH /orders/{id} moves an order to its next state"
    requirement: "Add the order state endpoint so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the order state endpoint changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The order state endpoint works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The order state endpoint is described in the changelog under the fulfilment heading"
    requires: ["orders-api-read"]
    repos: ["api"]
  - key: "orders-events"
    title: "An order state change publishes an OrderStateChanged event"
    requirement: "Add the order event so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the order event changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The order event works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The order event is described in the changelog under the fulfilment heading"
    requires: ["orders-api-write"]
    repos: ["api"]
  - key: "stock-reservation"
    title: "Stock is reserved when an order is confirmed"
    requirement: "Add the stock reservation so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the stock reservation changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The stock reservation works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The stock reservation is described in the changelog under the fulfilment heading"
    requires: ["orders-events", "ticket:#41"]
    repos: ["api"]
  - key: "stock-release"
    title: "Reserved stock is released when an order is cancelled"
    requirement: "Add the stock release so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the stock release changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The stock release works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The stock release is described in the changelog under the fulfilment heading"
    requires: ["stock-reservation"]
    repos: ["api"]
  - key: "shipping-labels"
    title: "A confirmed order gets a shipping label from the carrier"
    requirement: "Add the shipping label so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the shipping label changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The shipping label works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The shipping label is described in the changelog under the fulfilment heading"
    requires: ["orders-events"]
    repos: ["api"]
  - key: "shipping-tracking"
    title: "Carrier tracking updates move an order to shipped"
    requirement: "Add the tracking update so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the tracking update changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The tracking update works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The tracking update is described in the changelog under the fulfilment heading"
    requires: ["shipping-labels"]
    repos: ["api"]
  - key: "invoices-draft"
    title: "A shipped order produces a draft invoice"
    requirement: "Add the draft invoice so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the draft invoice changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The draft invoice works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The draft invoice is described in the changelog under the fulfilment heading"
    requires: ["shipping-tracking"]
    repos: ["api"]
  - key: "invoices-pdf"
    title: "A draft invoice renders as a PDF document"
    requirement: "Add the invoice PDF so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the invoice PDF changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The invoice PDF works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The invoice PDF is described in the changelog under the fulfilment heading"
    requires: ["invoices-draft"]
    repos: ["api"]
  - key: "notifications-mail"
    title: "The customer is mailed on every order state change"
    requirement: "Add the state mail so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the state mail changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The state mail works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The state mail is described in the changelog under the fulfilment heading"
    requires: ["orders-events"]
    repos: ["api"]
  - key: "web-order-list"
    title: "The order list shows the fulfilment state of each order"
    requirement: "Add the order list column so that the fulfilment of an order is visible and controlled end to end. The change lives in the web repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the order list column changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The order list column works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The order list column is described in the changelog under the fulfilment heading"
    requires: ["orders-api-read"]
    repos: ["web"]
  - key: "web-order-detail"
    title: "The order page shows the state history"
    requirement: "Add the order history panel so that the fulfilment of an order is visible and controlled end to end. The change lives in the web repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the order history panel changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The order history panel works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The order history panel is described in the changelog under the fulfilment heading"
    requires: ["orders-api-read"]
    repos: ["web"]
  - key: "web-state-actions"
    title: "The order page offers the next state as a button"
    requirement: "Add the state action button so that the fulfilment of an order is visible and controlled end to end. The change lives in the web repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the state action button changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The state action button works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The state action button is described in the changelog under the fulfilment heading"
    requires: ["orders-api-write", "web-order-detail"]
    repos: ["web"]
  - key: "web-stock-badge"
    title: "The order page shows whether stock is reserved"
    requirement: "Add the stock badge so that the fulfilment of an order is visible and controlled end to end. The change lives in the web repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the stock badge changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The stock badge works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The stock badge is described in the changelog under the fulfilment heading"
    requires: ["stock-reservation", "web-order-detail"]
    repos: ["web"]
  - key: "web-tracking-link"
    title: "The order page links the carrier tracking page"
    requirement: "Add the tracking link so that the fulfilment of an order is visible and controlled end to end. The change lives in the web repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the tracking link changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The tracking link works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The tracking link is described in the changelog under the fulfilment heading"
    requires: ["shipping-tracking", "web-order-detail"]
    repos: ["web"]
  - key: "web-invoice-download"
    title: "The order page offers the invoice PDF for download"
    requirement: "Add the invoice download so that the fulfilment of an order is visible and controlled end to end. The change lives in the web repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the invoice download changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The invoice download works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The invoice download is described in the changelog under the fulfilment heading"
    requires: ["invoices-pdf", "web-order-detail"]
    repos: ["web"]
  - key: "reports-fulfilment"
    title: "A daily report counts orders per fulfilment state"
    requirement: "Add the fulfilment report so that the fulfilment of an order is visible and controlled end to end. The change lives in the api repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the fulfilment report changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The fulfilment report works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The fulfilment report is described in the changelog under the fulfilment heading"
    requires: ["orders-migration", "ticket:19438"]
    repos: ["api"]
  - key: "docs-fulfilment"
    title: "The operator guide describes the fulfilment states"
    requirement: "Add the fulfilment guide so that the fulfilment of an order is visible and controlled end to end. The change lives in the web repository and keeps every existing caller working unchanged. Validation rejects a request that names an unknown order with a clear message: the order id and the reason. Every new behaviour is covered by tests that exercise the real persistence layer, not a mock of it. Nothing outside the fulfilment guide changes in this ticket; the follow-up tickets build on it once it has merged."
    done:
      - "The fulfilment guide works for a confirmed order and its result is stored with the order"
      - "A request naming an unknown order answers 404 with the order id in the message"
      - "Existing tests stay green and the new tests cover the success and the failure path"
      - "The fulfilment guide is described in the changelog under the fulfilment heading"
    requires: ["web-state-actions", "reports-fulfilment"]
    repos: ["web"]
```

Approve the list to file it; each ticket starts once the tickets it requires have merged.
