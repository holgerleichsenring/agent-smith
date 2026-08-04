# Migration: Courier -> Herald (in-process mediator) and BusWorks -> Conveyor (message broker)

<!-- Synthetic eval fixture (p0397). Fully invented libraries, repos and teams —
     no real product, customer or vendor identifiers. Sized to mirror a real
     per-variant migration manual (~2-3k words) so the plan call sees a
     realistically large multi-repo ticket. -->

You are migrating a .NET solution that spans **two repositories** —
`SampleServer` (the HTTP API) and `SampleWorker` (the background consumer
service) — away from two libraries whose licensing changed to a commercial
model this year. The stack switch is already decided and non-negotiable:

- **Courier -> Herald**: the in-process mediator. Herald is a
  source-generator-based mediator with the same conceptual model
  (requests, handlers, pipeline behaviors) but a different API surface.
- **BusWorks -> Conveyor**: the message-broker abstraction over RabbitMQ.
  Conveyor keeps the publish/consume model but replaces BusWorks'
  reflection-based endpoint discovery with explicit handler registration.

Both repositories must build, pass their test suites, and inter-operate on the
same broker topology after the migration. No behavior change is in scope —
this is a mechanical library swap with the API mappings documented below.

## Repository overview

`SampleServer` hosts the REST API. Controllers dispatch commands and queries
through Courier's `IDispatcher` into application-layer handlers; domain events
raised by aggregates are published to RabbitMQ through BusWorks'
`IBusControl` so that `SampleWorker` picks them up asynchronously.

`SampleWorker` is a headless worker service. It consumes the events published
by `SampleServer` via BusWorks consumer classes, and internally uses Courier
for its own command handling (each consumed message is turned into an
in-process command so retry semantics stay in one place).

Both repositories reference the shared contracts package
`Sample.Contracts` (message and event records). That package has **no**
dependency on either library and must not change.

## Part 1 — Courier -> Herald (both repositories)

### 1.1 Package changes

Remove in every project that references them:

- `Courier.Core`
- `Courier.Extensions.DependencyInjection`
- `Courier.Pipeline`

Add instead:

- `Herald` (runtime)
- `Herald.SourceGen` (analyzer/source generator, `PrivateAssets=all`)

### 1.2 API mapping

| Courier (old)                                   | Herald (new)                                        |
| ----------------------------------------------- | --------------------------------------------------- |
| `ICommand` / `ICommand<TResult>`                | `IRequest` / `IRequest<TResponse>`                  |
| `IQuery<TResult>`                               | `IRequest<TResponse>`                               |
| `ICommandHandler<TCommand>`                     | `IRequestHandler<TRequest>`                         |
| `ICommandHandler<TCommand, TResult>`            | `IRequestHandler<TRequest, TResponse>`              |
| `IQueryHandler<TQuery, TResult>`                | `IRequestHandler<TRequest, TResponse>`              |
| `IDispatcher.Send(cmd)`                         | `IMediator.Send(request, ct)`                       |
| `IDispatcher.Query(query)`                      | `IMediator.Send(request, ct)`                       |
| `INotification` / `INotificationHandler<T>`     | `INotification` / `INotificationHandler<T>` (same)  |
| `IPipelineStep<TIn, TOut>`                      | `IPipelineBehavior<TMessage, TResponse>`            |
| `services.AddCourier(asm)`                      | `services.AddHerald(o => o.ServiceLifetime = ...)`  |

Notes:

- Herald handler methods are named `Handle` and take a trailing
  `CancellationToken`; Courier's `ExecuteAsync` did not. Every handler
  signature changes. Thread the token through to repository and HTTP calls
  where one is already available in scope; do not invent new cancellation
  sources.
- Herald's `Handle` returns `ValueTask<TResponse>` rather than
  `Task<TResult>`. Handlers that simply `await` inner calls need only the
  signature change; handlers returning cached/synchronous results should
  return `ValueTask.FromResult(...)`.
- Courier's void-command handlers returned `Task`; Herald's equivalent is
  `IRequestHandler<TRequest>` whose `Handle` returns `ValueTask<Unit>`.
  Return `Unit.Value` at the end of each such handler.
- Herald discovers handlers at **compile time** via the source generator.
  The assembly-scanning call disappears; instead every project that contains
  handlers gets `services.AddHerald()` in its composition root and the
  generator emits the registrations. Handlers must be `public sealed` for
  the generator to pick them up — audit for `internal` handlers (there are
  several in `SampleWorker`) and widen them.

### 1.3 Pipeline behaviors

`SampleServer` has three Courier pipeline steps that must be ported to
Herald `IPipelineBehavior<,>` implementations, preserving order:

1. `RequestLoggingStep` — logs request name + elapsed time.
2. `ValidationStep` — runs the FluentValidation validators registered for the
   request type; aborts with a `ValidationFailedException` carrying all
   failures.
3. `TransactionStep` — opens the EF Core transaction for commands (not
   queries). Courier distinguished commands from queries by marker interface;
   Herald has no marker, so introduce `ITransactionalRequest` in the
   application layer and let the behavior no-op for requests that do not
   implement it.

Behavior order in Herald is registration order in the composition root —
pin it with an explicit comment and a test (see 3.2).

`SampleWorker` has one pipeline step (`ConsumeRetryStep`, exponential retry
around command handling). Port it the same way; the retry policy values move
unchanged from `CourierOptions.Retry` to the new `HeraldRetryOptions` bound
from the existing `Worker:Retry` configuration section.

### 1.4 Test doubles

Both test suites fake `IDispatcher` extensively. Replace the fakes with
Herald's `IMediator` interface; the existing hand-rolled
`RecordingDispatcher` in each suite becomes a `RecordingMediator`
implementing `IMediator.Send` overloads and recording the requests. Do not
introduce a compatibility shim that adapts the old fake interface — migrate
each test call site directly.

## Part 2 — BusWorks -> Conveyor (both repositories)

### 2.1 Package changes

Remove:

- `BusWorks`
- `BusWorks.RabbitMQ`
- `BusWorks.Extensions.DependencyInjection`

Add:

- `Conveyor`
- `Conveyor.Rabbit`

### 2.2 API mapping

| BusWorks (old)                                   | Conveyor (new)                                     |
| ------------------------------------------------ | -------------------------------------------------- |
| `IBusControl.Publish(msg)`                       | `IConveyorBus.PublishAsync(msg, ct)`               |
| `IConsumer<TMessage>` + `Consume(ctx)`           | `IMessageHandler<TMessage>` + `HandleAsync(msg, MessageContext, ct)` |
| `ConsumeContext<T>.Message`                      | first `HandleAsync` parameter                      |
| `ConsumeContext.Redeliver(...)`                  | `MessageContext.Requeue(delay)`                    |
| `services.AddBusWorks(cfg => cfg.AddConsumers())`| `services.AddConveyor(c => c.AddHandler<THandler>())` |
| `cfg.UsingRabbitMq((ctx, rmq) => ...)`           | `c.UseRabbit(o => ...)` bound from `Broker` config section |
| endpoint naming: kebab-case queue per consumer   | explicit: `c.AddHandler<T>(queue: "...")`          |

Notes:

- **Queue names must not change.** BusWorks derived kebab-case queue names
  from consumer class names; Conveyor requires the queue name explicitly at
  registration. Enumerate the queues currently in use (there are four
  consumers in `SampleWorker`) and pass the identical names so in-flight
  messages during a rolling deploy are not stranded.
- Conveyor serializes with `System.Text.Json` by default; BusWorks used its
  own envelope. Enable Conveyor's `RawJson` compatibility envelope so mixed
  fleets (old publisher, new consumer and vice versa) inter-operate during
  the rollout window.
- The outbox: `SampleServer` publishes domain events through BusWorks'
  in-memory outbox tied to the EF Core `SaveChanges`. Conveyor's
  `Conveyor.Outbox.EfCore` package provides the same guarantee; wire it to
  the existing `AppDbContext` and delete the hand-rolled
  `PendingEventRelay` hosted service — Conveyor's dispatcher replaces it.
- Dead-lettering: BusWorks moved poison messages to `<queue>_error` after
  5 failed deliveries. Configure Conveyor's `FailurePolicy` to the same
  count and suffix so operational dashboards keep working.

### 2.3 Consumer inventory (SampleWorker)

Migrate each consumer, keeping its queue name:

1. `OrderPlacedConsumer` — queue `order-placed`; turns the event into the
   in-process `RecordOrderCommand`.
2. `OrderCancelledConsumer` — queue `order-cancelled`; command
   `CancelOrderCommand`.
3. `InventoryAdjustedConsumer` — queue `inventory-adjusted`; command
   `AdjustInventoryCommand`.
4. `CustomerNotifiedConsumer` — queue `customer-notified`; fire-and-forget
   notification through the mediator (`INotification`).

Each consumer's `Consume` body currently wraps dispatch in a try/catch that
calls `Redeliver` with backoff; after 1.3's `ConsumeRetryStep` port the
catch block collapses to `MessageContext.Requeue` on the terminal failure
path only.

### 2.4 Publisher inventory (SampleServer)

`OrderService` and `InventoryService` publish five event types via
`IBusControl.Publish`; swap to `IConveyorBus.PublishAsync` with the ambient
cancellation token. The `DomainEventDispatcher` bridges aggregate events to
the bus — it moves to the Conveyor outbox (2.2) and loses its manual
transaction handling.

## Part 3 — Verification

### 3.1 Builds and tests

- Both repositories build with zero warnings from the new analyzers.
- All existing unit and integration tests pass after the mechanical swap.
- No references to `Courier.*` or `BusWorks.*` remain in any csproj,
  using directive, or configuration file.

### 3.2 New tests required

- A behavior-order test in `SampleServer` asserting Logging -> Validation ->
  Transaction execution order through a probe request.
- A queue-name pin test in `SampleWorker` asserting the four registered
  queue names match the documented values exactly.
- An outbox round-trip integration test in `SampleServer`: command commits ->
  event lands on the bus fake; rollback -> nothing published.

### 3.3 Configuration

- `Broker` configuration section shape stays identical (`Host`, `VirtualHost`,
  `Username`, `PasswordSecret`, `PrefetchCount`); Conveyor binds it via
  `UseRabbit`. Secrets stay environment-provided; nothing moves into files.
- Delete the `BusWorks` feature-flag toggles left from the original rollout
  (`Features:UseBusWorksOutbox`) — dead after this migration.

## Part 4 — Migration order and edge cases

### 4.1 Recommended order of work

Do the mediator swap first, broker swap second, one repository at a time,
committing after each green build:

1. `SampleServer`: Courier -> Herald (packages, handler signatures,
   behaviors, composition root, test doubles). The API surface is the larger
   of the two swaps here — roughly 30 handlers and 3 behaviors.
2. `SampleServer`: BusWorks -> Conveyor (publishers, outbox, configuration).
3. `SampleWorker`: Courier -> Herald (about 12 handlers, 1 behavior, the
   `internal` handler audit from 1.2).
4. `SampleWorker`: BusWorks -> Conveyor (the four consumers, queue-name
   pins, failure policy).

The order matters because the worker's consumers dispatch into its mediator:
migrating the broker layer first would force touching each consumer twice.

### 4.2 Known sharp edges

- **Streaming queries.** Two queries in `SampleServer`
  (`ExportOrdersQuery`, `ExportInventoryQuery`) return
  `IAsyncEnumerable<T>` through Courier's `IStreamQueryHandler`. Herald
  models these as `IStreamRequest<T>` / `IStreamRequestHandler<TRequest, T>`
  with `IMediator.CreateStream(request, ct)`. The controller actions that
  iterate the stream change accordingly. Do not buffer the stream to a list
  to dodge the API difference — the exports are large by design.
- **Generic handler.** `AuditLogHandler<T>` handles every command via
  Courier's open-generic registration. Herald's source generator does not
  register open generics; replace it with a pipeline behavior appended after
  `TransactionStep` that writes the audit entry post-commit. This is the one
  place where the migration is a redesign rather than a mapping — keep the
  behavior small and put the reasoning in the commit message.
- **Scoped publisher.** `InventoryService` resolves `IBusControl` from a
  scope created inside a background timer. Conveyor's `IConveyorBus` is a
  singleton and safe to inject directly; delete the scope plumbing there.
- **Test parallelism.** The worker's integration tests spin an in-memory
  BusWorks harness per test class. Conveyor's `Conveyor.Testing` package
  provides `InMemoryConveyorHarness`; it is NOT parallel-safe across
  collections, so mark the affected test classes with a shared xunit
  collection fixture the way the server suite already does for its database
  fixture.
- **Message headers.** `CorrelationId` propagation currently relies on
  BusWorks' automatic header forwarding. Conveyor forwards nothing by
  default: register its `HeaderPropagation` middleware on both publish and
  consume paths and pin it with an assertion inside the round-trip test
  (3.2) so a silent drop of correlation ids cannot land.

### 4.3 What is explicitly out of scope

- No topology redesign: exchanges, bindings and queue names stay exactly as
  they are today.
- No handler consolidation or renaming beyond what the API mapping forces.
- No upgrade of unrelated packages, even where the new analyzers suggest it.
- No change to `Sample.Contracts` — if a mapping seems to require a contract
  change, stop and flag it instead of changing the package.

### 4.4 Rollout and rollback

Deploy `SampleWorker` first (consumers tolerate both envelopes once the
compatibility setting from 2.2 is active), then `SampleServer`. Rollback is
per-repository: because queue names and the envelope are unchanged, the old
build of either service can be redeployed against the same broker without
draining queues. Document the verified rollback step in each repository's
deploy notes as part of this ticket.

## Acceptance criteria

1. Neither repository references Courier or BusWorks packages, types, or
   configuration keys.
2. All four worker queues keep their exact names; the error-queue suffix and
   redelivery count are unchanged.
3. Handler and consumer behavior is unchanged: same retries, same
   transaction boundaries, same outbox guarantee.
4. The three new tests from 3.2 exist and pass; both suites are green.
5. A rolling deploy with mixed versions does not lose or duplicate messages
   (compatibility envelope enabled and verified by the round-trip test).
