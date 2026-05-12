# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repo is

Tesla Order Tracker — a public hobby service that lets users track their Tesla order (VIN, delivery window, etc.) via the unofficial Tesla Owner API. **Backend in .NET 10 (Azure Functions isolated worker)**, **frontend in TypeScript + React 18 + Vite + Tailwind v4 (PWA)**, **persistence via Azure Table Storage**, **push notifications via Web Push (VAPID)**. Plan progresses by weekend sprints; current state: Sprint 1 + 2 implemented. The authoritative project plan lives at `~/.claude/plans/roll-och-kontext-du-vivid-tulip.md` — consult it before designing anything new.

## Common commands

All paths assume the repo root as cwd.

```powershell
# Build whole backend solution
dotnet build backend/TeslaTracker.sln

# Run all backend tests
dotnet test backend/TeslaTracker.sln

# Run a single test project
dotnet test backend/tests/TeslaTracker.Domain.Tests/TeslaTracker.Domain.Tests.csproj

# Run a single test class / method
dotnet test backend/TeslaTracker.sln --filter "FullyQualifiedName~OrderRepositoryTests"
dotnet test backend/TeslaTracker.sln --filter "FullyQualifiedName=TeslaTracker.Domain.Tests.Orders.OrderTests.ApplySnapshot_With_New_Vin_Raises_VinAssigned"

# Frontend (must run from frontend/ — use Push-Location)
Push-Location frontend; npm run dev; Pop-Location
Push-Location frontend; npm run build; Pop-Location
Push-Location frontend; npm run lint; Pop-Location

# Integration tests in Infrastructure.Tests require Azurite running.
# Start it in a separate terminal (does not auto-start):
azurite --silent --location ./.azurite --debug ./.azurite/debug.log
# Tests gated by [RequiresAzuriteFact] skip automatically if Azurite is unreachable.

# DevelopmentTokenProtector needs a User Secret for AES-GCM key:
dotnet user-secrets set "Crypto:DevKey" "<base64-encoded 32 bytes>" --project backend/src/TeslaTracker.Infrastructure
```

## High-level architecture

### Monorepo layout

- `backend/` — .NET 10 solution (`TeslaTracker.sln`), 4 source projects + 3 test projects, SDK pinned via `global.json`, shared properties in `backend/Directory.Build.props` (Nullable, ImplicitUsings, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`).
- `frontend/` — Vite + React 18 + TypeScript + Tailwind v4 + PWA. Service worker source in `src/service-worker/sw.ts` (handles push + notificationclick). Tailwind v4 uses the `@tailwindcss/vite` plugin (no `tailwind.config.js`); theme tokens live in `src/index.css` under `@theme`.
- `infra/` — reserved for Bicep templates (Sprint 6).
- `docs/security.md` — threat model. Update per sprint when new attack surfaces are added.

### Clean Architecture + DDD layering

References flow strictly inward — verify by reading `.csproj` files:

```
Functions  ──→  Infrastructure  ──→  Application  ──→  Domain
   (adapters)      (Azure/Tesla)        (use cases)       (pure)
```

- **`TeslaTracker.Domain`** — zero IO, no NuGet deps beyond BCL. Houses aggregates (`Orders/Order.cs`, `Notifications/PushChannel.cs`), value objects (`OrderId`, `Vin`, `DeliveryWindow`, `OrderSnapshot`, `TrackingSecret`, `PushEndpoint`), domain events (`Events/`), specifications (`Specifications/DueForSyncSpec.cs`), repository ports (`IOrderRepository`, `IPushChannelRepository`), and shared building blocks (`SeedWork/AggregateRoot`, `IDomainEvent`, `Result<T>`, `ISpecification`).
- **`TeslaTracker.Application`** — orchestration only. Use case handlers under `Orders/Commands/RegisterOrderTracking/`, `SyncOrderWithTesla/`, `StopTracking/` (each its own folder with Command + Handler + Validator). Ports for things crossing boundaries: `ITeslaOrderGateway` (Tesla ACL contract), `ITokenProtector`, `IClock`, `IUnitOfWork`, `IDomainEventDispatcher`, `IDomainEventHandler<TEvent>`, `IAggregateTracker`, `IRateLimiter`.
- **`TeslaTracker.Infrastructure`** — implements every port. `Tesla/` is the Anti-Corruption Layer (`TeslaSnapshotTranslator` is the only thing that knows Tesla's DTO shape — those types never leak past `Tesla/Dto/`). `Storage/` holds Table entities, mappers, repositories, `UnitOfWork`, `AggregateTracker`, and the `OrderEventHistoryProjection` (a read-model handler). `Crypto/` handles AES-GCM envelope encryption via `EnvelopeCipher` + `DevelopmentTokenProtector` (dev only — emits warning on every call) / `KeyVaultTokenProtector` (Sprint 6 stub).
- **`TeslaTracker.Functions`** — Azure Functions isolated worker. HTTP triggers + timer (Sprint 3, not yet implemented).

### Domain event flow

Aggregates extend `AggregateRoot`, which collects events via `RaiseEvent(...)` in `PendingEvents`. Repositories call `IAggregateTracker.Track(aggregate)` after each `AddAsync`/`UpdateAsync`. Use case handlers call `IUnitOfWork.SaveChangesAsync()` after persisting, which iterates tracked aggregates, hands events to `InMemoryDomainEventDispatcher`, then clears. The dispatcher resolves `IDomainEventHandler<TEvent>` implementations from DI by reflection. `OrderEventHistoryProjection` is one handler that fans out across all 4 order events to a read-model table (RowKey is `invertedTicks_guid` to avoid collision when multiple events share `OccurredAt`).

**Eventual consistency caveat:** Table Storage has no cross-partition transactions. If the dispatcher fails after persistence succeeds, the read model diverges from the aggregate. Acceptable for v1; outbox pattern is the escape hatch if abuse signals appear.

### Azure Table Storage layout

Four tables, names defined in `Storage/TeslaTrackerTables.cs`:

| Table | PartitionKey | RowKey | Notes |
|---|---|---|---|
| `orders` | `ACTIVE` or `ARCHIVED` | OrderId (RN-number) | ETag for optimistic concurrency. `Order.Stop()` / `MarkTokenRevoked()` move rows between partitions in `OrderRepository.UpdateAsync`. |
| `orderhistory` | OrderId | `(MaxTicks - OccurredAt.Ticks).D19 + "_" + Guid.NewGuid().N` | Read-model projection written by `OrderEventHistoryProjection`. Newest-first ordering. |
| `pushchannels` | OrderId | `SHA256(endpoint).ToHex()` | Deterministic hash → upsert without duplicates. |
| `ratelimit` | `ip:1.2.3.4` or `order:RN...` | `yyyyMMddHHmm` | Per-minute sliding window. |

### Security model (token handling)

Tesla refresh tokens grant full vehicle control (locks, climate). Plaintext exists only in `TeslaCredential.RefreshToken` (use case parameter) and `ITokenProtector.UnprotectAsync` return values — never on the `Order` aggregate, never serialized, never logged. Aggregates only hold `TrackingSecret` (ciphertext blob + Key Vault key version reference). When changing this area, re-read `docs/security.md` and run all Crypto tests.

## Code conventions

- **C# `Result<T>`** (`Domain/SeedWork/Result.cs`) for expected failures (token revoked, order not found, invalid input). Exceptions reserved for invariant violations (`InvariantViolationException`).
- **Value objects** = `sealed record` with private constructor + static `Create(...)` returning `Result<T>`. No `new` from outside the type.
- **Aggregates expose no setters.** State changes via methods that raise events (e.g., `Order.ApplySnapshot`).
- **File-scoped namespaces** (enforced as warning), `var` only when type is obvious, expression-bodied when single-line.
- **No MediatR.** Use the internal `ICommandHandler<TCmd, TResult>` / `ICommandHandler<TCmd>` convention.
- **No persistence attributes in Domain.** Mappers in `Infrastructure/Storage/Mappers/` translate between aggregates and Table-DTOs.
- **FluentAssertions pinned to 7.x.** Do not upgrade to 8+ — licensing changed to commercial in v8.
- **TypeScript frontend:** `type` aliases over `interface` (avoids declaration merging), Zod for runtime validation of API responses (`z.infer` provides types), strict mode enforced by Vite template.
- **InternalsVisibleTo** is enabled between `TeslaTracker.Infrastructure` and `TeslaTracker.Infrastructure.Tests` so ACL internals (`EnvelopeCipher`, `TeslaSnapshotTranslator`) stay `internal` while remaining testable.

## DI lifetimes (Infrastructure)

Defined in `Infrastructure/DependencyInjection.cs`:
- **Singleton:** `TableServiceClient`, `IClock` (`SystemClock`), `TeslaSnapshotTranslator`
- **Scoped:** `IAggregateTracker`, `IUnitOfWork`, `IDomainEventDispatcher`, repositories, gateway, token protector, event handlers
- **Transient (via `AddHttpClient<TeslaOwnerApiClient>`):** Tesla HTTP client with Polly v8 standard resilience handler

`AddDevelopmentTokenProtector()` and `AddKeyVaultTokenProtector()` are opt-in — pick exactly one per host.

## Testing conventions

- **xUnit + FluentAssertions 7.2 + NSubstitute** for unit tests. `FakeClock` in `Application.Tests/TestSupport/` controls time.
- **WireMock.Net** for HTTP integration tests (mocks Tesla API).
- **Azurite** for Table Storage integration tests. Tests using `[RequiresAzuriteFact]` skip gracefully when Azurite is unreachable (so CI without Docker still passes). `AzuriteFixture` is an `IAsyncLifetime` that creates tables and cleans rows between tests.
- **End-to-end tests** in `Infrastructure.Tests/EndToEnd/` build the full DI graph via `TestServiceFactory.Build(wireMockUri)` and exercise real Application handlers against Azurite + WireMock.

## Working in this repo

- The user codes in Swedish and prefers Swedish communication in conversation; **commit messages are English imperative form**.
- The user has approved autonomous execution after a plan is approved: run `dotnet`/`npm`/`git` commands directly without confirming each step. Stop only for destructive operations, design changes the plan doesn't cover, or security-relevant decisions.
- Plans live under `~/.claude/plans/` — read the existing plan before extending. Update the plan file when scope changes, not after the fact.
- Memory notes (`~/.claude/projects/.../memory/`) capture user preferences and reference points; update them when something durable changes.
