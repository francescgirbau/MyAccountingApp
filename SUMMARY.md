# MyAccountingApp

Personal finance tracking app. Import bank CSV + broker statements, FIFO cost basis, annual summaries, full CRUD via Blazor WASM UI.

## Tech Stack
- **Backend**: .NET 9 Minimal API (C#)
- **Frontend**: Blazor WASM (MudBlazor 9.0.6)
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
- **Conversions**: Currency rate management
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
| GET | `/api/conversions` | Currency conversions |
| GET | `/api/summary` | Annual summaries |
| POST | `/api/import` | Folder import |
| POST | `/api/import/upload` | CSV file upload |

## Persistence
- **JSON files** mounted as Docker volumes (`./data:/app/data`)
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
- `CURRENCY_API_KEY` — API key for exchangerate.host

## Current State (July 2026)
- 134 tests, 81.94% combined coverage
- Branch: `58-transaction-crud` (PR #5 open)
- CI: GitHub Actions, Release build with `-warnaserror`

## Known Issues
- WASM browser cache: use Ctrl+F5 or incognito after rebuild
- `data/transactions.json` had duplicates from old corruption recovery (now fixed with dedup)
