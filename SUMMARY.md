# MyAccountingApp

Personal finance tracking app. Import bank CSV + broker statements, FIFO cost basis, annual summaries, full CRUD via Blazor WASM UI.

## Tech Stack
- **Backend**: .NET 9 Minimal API (C#)
- **Frontend**: Blazor WASM (MudBlazor 9.6.0)
- **Data**: JSON files (`data/transactions.json`, `data/portfolio.json`, `data/conversions.json`)
- **Infrastructure**: Docker multi-stage (WASM served from API `wwwroot/`)

## Architecture

```
MyAccountingApp.Domain     → Entities, ValueObjects, Interfaces
MyAccountingApp.Core       → Repositories (JSON, InMemory, Composite), Services, Agents
MyAccountingApp.Application → DTOs, DTO mapper, Services (import, portfolio, summary)
MyAccountingApp.Api       → Minimal API endpoints, DI setup
MyAccountingApp.Web       → Blazor WASM UI (MudBlazor pages)
```

## Key Features
- **Import**: Bank CSV + broker statements via folder scan or file upload (`POST /api/import/upload`)
- **Transactions**: List with filters (year, category, search), sorting, pagination; full CRUD (create/edit/delete)
- **Asset Transactions**: CRUD with Symbol, Quantity, Type fields
- **Portfolio**: Positions table with expandable open lots, realized/unrealized P&L, coloring
- **Annual Summary**: `GET /api/summary` and `GET /api/summary/{year}`
- **Conversions**: Currency rate caching (Frankfurter by default, exchangerate.host optional). Timeseries fetch, lazy per-day lookups with stale fallback, pending queue for dates that could not be fetched, startup gap sync and status endpoint
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
  - `data/api_quota.json` — API quota usage per period
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

## Key Environment Variables
- `CURRENCY_API_KEY` — API key for exchangerate.host (only when `CurrencyApi:Provider` is set to `ExchangeRateHost`)

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

On startup the API backfills the last 90 days if no conversions are stored; otherwise it syncs the gap between the last cached day and yesterday (`POST /api/conversions/sync` behavior, chunked by `MaxTimeseriesDays`). When the provider fails or the quota is exhausted for a requested date, the date is queued in `pending_conversions.json` and the closest cached conversion is returned marked as **stale**; `POST /api/conversions/process-pending` retries the queue once quota is available again. HTTP calls to the provider time out after 30 seconds.

## Current State (August 2026)
- 298 tests, 83.3% combined coverage
- CI: GitHub Actions, Release build with `-warnaserror`, StyleCop gate (0 warnings enforced), coverage gate ≥ 80%

## Known Issues
- WASM browser cache: use Ctrl+F5 or incognito after rebuild
- `data/transactions.json` had duplicates from old corruption recovery (now fixed with dedup)
