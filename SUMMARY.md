# MyAccountingApp

Personal finance tracking app. Import bank CSV + broker statements, FIFO cost basis, annual summaries, full CRUD via Blazor WASM UI.

## Tech Stack
- **Backend**: .NET 9 Minimal API (C#)
- **Frontend**: Blazor WASM (MudBlazor 9.6.0)
- **Data**: JSON files (`data/transactions.json`, `data/portfolio.json`, `data/conversions.json`)
- **Infrastructure**: Docker multi-stage (WASM served from API `wwwroot/`)

## Architecture (layer map)

```
MyAccountingApp.Domain      → Entities, ValueObjects, Enums, Interfaces (no I/O, no HTTP)
MyAccountingApp.Application  → Services (conversions, import, portfolio, summary), DTOs, orchestration
MyAccountingApp.Core         → Infrastructure implementations
│   Core/Persistence         → JSON + in-memory repositories (namespaces MyAccountingApp.Core.Persistence)
│   Core/Http                → Currency (Frankfurter, ExchangeRateHost) + Market (Yahoo) clients
│   Core/Imports             → IBKR / Degiro / Revolut / AbnAmro parsers + Common CSV helpers
│   Core/DTOs, Core/Models   → Response DTOs and IBKR record models
MyAccountingApp.Api          → Minimal API endpoints (Endpoints/), DI wiring, pipeline, startup sync
MyAccountingApp.ConsoleApp   → Manual CLI entry point (imports, conversions)
MyAccountingApp.Web          → Blazor WASM UI (MudBlazor pages)
tests/                       → xUnit suites: Domain.Tests, Application.Tests, Core.Tests, Api.Tests
```

Rules of thumb: Domain has no external dependencies; Application depends only on Domain + interfaces; Core holds all concrete I/O and third-party calls; Api is the composition root. Tests of Application use fakes from `MyAccountingApp.TestUtilities` (no JSON repositories, no real HTTP).

## Key Features
- **Import**: Bank CSV + broker statements via folder scan or file upload (`POST /api/import/upload`)
- **Transactions**: List with filters (year, category, search), sorting, pagination; full CRUD (create/edit/delete)
- **Asset Transactions**: CRUD with Symbol, Quantity, Type fields
- **Portfolio**: Positions table with expandable open lots, realized/unrealized P&L, coloring
- **Annual Summary**: `GET /api/summary` and `GET /api/summary/{year}`
- **Conversions**: Currency rate caching (Frankfurter by default, exchangerate.host optional). Timeseries fetch, lazy per-day lookups with stale fallback, pending queue for dates that could not be fetched, startup gap sync and status endpoint. **BTC is not covered by any provider.**
- **Market Prices**: Yahoo Finance integration via `IMarketPriceService`
- **Position Engine**: FIFO cost basis, P&L calculation

## API Endpoints
| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/transactions` | List all transactions |
| POST | `/api/transactions` | Create transaction |
| PUT | `/api/transactions/{id}` | Update transaction |
| DELETE | `/api/transactions/{id}` | Delete transaction |
| GET | `/api/asset-transactions` | List asset transactions |
| POST | `/api/asset-transactions` | Create asset transaction |
| PUT | `/api/asset-transactions/{id}` | Update asset transaction |
| DELETE | `/api/asset-transactions/{id}` | Delete asset transaction |
| GET | `/api/portfolio` | Portfolio positions |
| GET | `/api/conversions` | Currency conversions (timeseries lookups) |
| GET | `/api/conversions?date=YYYY-MM-DD` | Single conversion, fetched on demand and cached (stale fallback when the provider fails or quota is exhausted) |
| GET | `/api/conversions/status` | Provider, cached day count, last cached date, pending queue size |
| GET | `/api/conversions/quota` | Quota usage, safety margin, availability period, pending queue size |
| POST | `/api/conversions/sync` | Backfill a date range in one request `{from, to}` |
| POST | `/api/conversions/process-pending` | Retry dates in the pending queue |
| GET | `/api/summary` | Annual summaries |
| POST | `/api/import` | Folder import |
| POST | `/api/import/upload` | CSV file upload |

## Persistence
- **JSON files** mounted as Docker volumes (`./data:/app/data`)
  - `data/transactions.json` — transactions
  - `data/portfolio.json` — asset transactions (portfolio)
  - `data/conversions.json` — cached conversion rates
  - `data/api_quota.json` — API quota usage per period (exchangerate.host only)
  - `data/pending_conversions.json` — dates waiting for quota to be available
- Atomic writes: write to `.tmp`, then `File.Move` (prevents corruption)
- Auto-recovery: `GetAll()` truncates to last valid `}` if JSON is corrupted
- Deduplication: `GetAll()` removes duplicate IDs on read

## Running
```bash
docker compose up --build -d
```
- Container: `myaccountingapp-api`
- URL: `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger`
- Logs: mounted to `./logs/` (Serilog, 7-day retention)

## How to run without CURRENCY_API_KEY
Frankfurter is the default provider and requires **no key, no account, no quota**. Just run the API/ConsoleApp as-is.
`CURRENCY_API_KEY` is only needed when `CurrencyApi:Provider` is set to `ExchangeRateHost`.

## Currency Options (`appsettings.json` → `CurrencyApi`)
| Key | Default | Description |
|---|---|---|
| `CurrencyApi:Provider` | `Frankfurter` | Rate provider: `Frankfurter` (no key required) or `ExchangeRateHost` (key required) |
| `CurrencyApi:BaseUrl` | `https://api.frankfurter.dev` | External rate provider |
| `CurrencyApi:ApiKey` | *(from `CURRENCY_API_KEY`)* | Provider API key |
| `CurrencyApi:RequestsLimit` | `100` | Monthly request limit (unlimited for Frankfurter) |
| `CurrencyApi:SafetyMargin` | `10` | Requests reserved as safety margin |
| `CurrencyApi:BackfillDaysOnFirstRun` | `90` | Days backfilled on first run when the repository is empty |
| `CurrencyApi:MaxTimeseriesDays` | `365` | Max days a single timeseries request may cover |

On startup the API backfills the last 90 days if no conversions are stored; otherwise it syncs the gap between the last cached day and yesterday, chunked by `MaxTimeseriesDays`. When the provider fails or the quota is exhausted for a requested date, the date is queued in `pending_conversions.json` and the closest cached conversion is returned marked as **stale**; `POST /api/conversions/process-pending` retries the queue once quota is available again. Quota is consumed only after a successful API response (P0). HTTP calls to the providers use `IHttpClientFactory` with a retry policy for transient failures (408/5xx/timeout, 3 attempts exponential backoff; no retry on 429/4xx so quota errors surface immediately).

## Testing
- **339 tests**, combined coverage above the 80% gate
- `tests/MyAccountingApp.Api.Tests` boots the real API with `WebApplicationFactory<Program>` (in-memory fakes, no external HTTP) and exercises every endpoint group
- CI: GitHub Actions, Release build with `-warnaserror` and StyleCop gate (0 warnings), coverage gate ≥ 80%

## Improvement series P0–P2 (closed, August 2026)
- **P0** (#94–#97): quota consumed only on API success, repository file renames, `CurrencyRateService` naming, docs
- **P1.1–P1.5** (#98–#102): DTO moves, `IHttpClientFactory` for currency clients, `Program.cs` split into DI/pipeline/endpoints, `GetLatestOnOrBefore` fallback, ConsoleApp with Frankfurter default
- **P1.6** (#103): API integration tests with `WebApplicationFactory`
- **P2.1** (#104): Polly retry policy for currency HttpClients
- **P2.2** (#105): Application.Tests freed from JSON repository implementations
- **P2.3a** (#106): Core reorganized into `Persistence` / `Http` / `Imports` folders and namespaces
- **P2.4** (#107): structured logs for the conversion cache/fetch paths
- **P2.5** (this PR): architecture docs and series close

The **P0–P2 technical series is closed**: the repository is in a "stable for product" state. Everything below is product backlog, not technical debt from the initial review.

## Product Roadmap (backlog, not committed)
| ID | Topic | When |
|---|---|---|
| R1 | CoinGecko provider for BTC (fiat-only today; BTC explicitly excluded) | When crypto valuation is needed |
| R2 | Extend supported currencies (CZK, PLN, …) | When real transactions require it |
| R3 | Multi-base currency (not only EUR) | If the accounting model demands it |
| R4 | Self-hosted Frankfurter | Volume / air-gap |
| R5 | SQL instead of JSON persistence | If volume or concurrency justifies it |
| R6 | API auth | If exposed beyond localhost |
| R7 | Detailed metrics / health | If deployed 24/7 |

## Known Issues
- WASM browser cache: use Ctrl+F5 or incognito after rebuild
- `data/transactions.json` had duplicates from old corruption recovery (now fixed with dedup)
