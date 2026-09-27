# MyAccountingApp — Architecture

Companion documents:

- `docs/VISION.md` — *why*: principles, priorities, philosophy.
- `docs/MIGRATION.md` — *how*: data playbook, storage inventory, recipes for safe change.
- This file — *where we are going*: target structure, dependency rules, constitution.

None of this is a mandate to do a big-bang refactor. The project evolves opportunistically:
architecture rides on features (or on a real driver, such as a second persistence layer),
never the other way around.

---

## Target structure

```text
MyAccountingApp
│
├── Domain         financial concepts + business rules (no external deps)
├── Application    use cases, queries, calculations (depends on Domain)
├── Infrastructure persistence, imports, market data, currency, vault (implements Domain/Application abstractions)
├── Contracts      API ↔ Web DTOs, single source of truth, no logic
├── Api            HTTP endpoints (thin)
├── Web            Blazor WASM UI
└── ConsoleApp     maintenance/ops tool (dev only)
```

## Current project map (verified 2026-09)

| Project | Role today | References |
|---|---|---|
| **Domain** | Entities, value objects, enums, and the repository abstractions (`ITransactionRepository`, `IPortfolioRepository`, `IOptionTransactionRepository`, `ILoanRepository`, …) | none |
| **Core** | **De facto infrastructure**: persistence (`Json*`/`Composite` repositories, `EncryptedJsonFileStorage`, `VaultService`), broker imports (IBKR, SelfBank, Cobas, MyInvestor, Degiro, Revolut, AbnAmro, Coinbase, generic bank/fund CSVs), market data (Yahoo, Frankfurter/exchangerate.host + quota), Vault | Domain |
| **Application** | Orchestration: import service, command/query services, calculations (positions, split adjustments, validation) | Core, Domain, Contracts |
| **Contracts** | Wire shapes (DTOs, request records): single source of truth for the API/Web payloads, no logic | none |
| **Api** | Minimal API endpoints, mostly thin; returns `Contracts` shapes | Core, Application, Contracts |
| **Web** | Blazor WASM + MudBlazor; talks to the API over HTTP; consumes `Contracts` shapes only (no `Application`/`Domain` reference) | Contracts |
| **ConsoleApp** | Ops/maintenance tool | Core, Application |

**Key observations**

1. `Domain` already holds the repository *abstractions* — the dependency direction is healthy.
2. `Core` is effectively the future `Infrastructure`: persistence, imports and external providers all live behind interfaces already. Splitting/renaming it is mostly mechanical — do it **only when there is a driver** (new storage, new broker kind, or before a big feature that touches several of those areas).
3. `Web` has depended on `Application` directly since the beginning. **PR-1 (2026-09)** created `Contracts` and moved every wire shape there, ending the hand-duplicated DTOs. **PR-2 (2026-09)** dropped the `Web → Application` reference entirely: the presentation helpers moved into the Web (`Web/Services`, `DataQuality.razor` works on `Contracts` DTOs) and the utility `TransactionCategoryFilter` stays in Application because the Api uses it (the Web inlines that one-liner filter locally).
4. The DTO wire is now single-sourced in `Contracts` and protected by `Api.Tests` (JSON-key assertions); no more drift by hand.

## Dependency rules (target)

1. `Domain` has no project references beyond the BCL.
2. `Application` and `Infrastructure` depend on `Domain`, never the reverse (`Domain ❌ Infrastructure`).
3. `Infrastructure` implements the repository/provider abstractions defined in `Domain`/`Application`.
4. `Api` is thin: no business logic; uses Application use cases; returns `Contracts` DTOs, never raw Domain entities.
5. `Web` depends on `Contracts` shapes (and its own local services), never on `Domain` or `Infrastructure`.
6. `Contracts` contains pure wire shapes — no logic, no Domain references.

## Constitution (rules for every future change)

> 1. **No destructive data migration.** A migration that can lose information is not a migration.
> 2. **Every schema change is backward-compatible or has an explicit migration.**
> 3. **Existing data must be loadable after every refactor** — compatibility tests prove it.
> 4. **Refactors and feature changes are separate PRs.**
> 5. **Every persistence refactor ships compatibility tests.**
> 6. **Domain calculations are deterministic.** AI/LLMs may classify or parse, never compute money.
> 7. **Infrastructure details do not leak into Domain** (no JSON, HTTP, file paths, providers in Domain).
> 8. **API contracts do not expose Domain entities raw**; the wire shape lives in `Contracts`.
> 9. **Derived data is recalculable from persisted facts.** If you can delete it and rebuild it from the source rows, it is derived state.
> 10. **Never fix architecture by rewriting working financial data.**

Two rules specific to this project:

> 11. **Imports are idempotent.** Re-importing the same file adds zero new rows (content fingerprint + report, PR #194).
> 12. **Facts stay, derived gets recomputed.** Adjusting a lot (e.g. split factor) changes `Quantity` while preserving `Amount`; everything derived — `UnitaryCost`, positions, P/L — is recomputed, never edited by hand.

## Facts vs derived state

**Facts (persisted):**

- `Transaction` (cash, incl. FX legs with pair identity)
- `AssetTransaction` (portfolio lots)
- `OptionTransaction`
- `Loan` + `LoanMovement`
- `Conversion` quotes (a cache, rebuildable from the provider)
- `MarketQuote` daily prices (a lazy cache: what was used on each day, rebuildable from the provider)

**Derived (computed, never stored):**

- positions (`PositionEngine`), `UnitaryCost()`
- unrealized / realized P/L, portfolio valuation in EUR
- cash-flow buckets, dashboard aggregates, realized-gains report

This split is what keeps the data safe: the source of truth stays human-readable, and any
derived bug is fixed in code, not in data.

## Where we are vs target

| Step | Value | When to do it |
|---|---|---|
| `Contracts` project (end DTO duplication) | **Done — PR-1 (2026-09):** wire shapes live in `Contracts`; Web consumes them; JSON keys enforced by `Api.Tests` | — |
| Web → contracts-only (drop the `Application` reference) | **Done — PR-2 (2026-09):** Web depends on `Contracts` only; helpers moved to `Web/Services`; `DataQuality.razor` DTO-based | — |
| `Core` → `Infrastructure` split (Persistence / Imports / MarketData / Currency / Vault) | Low–medium (mechanical) | Only with a driver: new storage, new broker, or large feature crossing those areas |
| Api thinning (endpoints that reach into repositories directly behind use cases) | Medium | Opportunistically, per endpoint, as features require |
| Web component splitting (big `.razor` pages → focused components) | Medium | Behavior-preserving, one page per PR |
| Golden datasets + persistence compatibility tests | High | Before the next persistence change (`docs/MIGRATION.md`) |
| SQLite behind the persistence abstraction | — | Only if JSON stops being enough (volume/query/concurrency) — not now |
| AI-assisted classification | — | Last; deterministic core first (`docs/VISION.md`) |