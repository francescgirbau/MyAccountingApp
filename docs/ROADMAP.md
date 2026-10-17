# MyAccountingApp — Roadmap

| # | Title | Status |
|---|---|---|
| 35 | docs: add VISION.md and ROADMAP.md | 🟢 Done |
| 36 | feat: scaffold Minimal API project with healthcheck | 🟢 Done |
| 37 | feat: add GET /transactions endpoint | 🟢 Done |
| 38 | feat: add GET /asset-transactions endpoint | 🟢 Done |
| 39 | feat: add GET /conversions endpoint | 🟢 Done |
| 40 | feat: add POST /import endpoint and extract orchestration | 🟢 Done |
| 41 | feat: add GET /portfolio/{symbol} endpoint (query-time) | 🟢 Done |
| 42 | refactor: add IBrokerImportService interface | 🟢 Done |
| 43 | feat: add validation pipeline with result object | 🟢 Done |
| 44 | test: add property-based tests for FX and UnitaryCost | 🟢 Done |
| 45 | feat: add Frankfurter as default currency provider | 🟢 Done |
| 46 | feat: quota-aware currency rate caching with timeseries sync | 🟢 Done |
| 47 | style: fix StyleCop violations and make the CI gate real | 🟢 Done |
| 48 | feat: currency conversions stabilization (resilience, gap sync, status endpoint) | 🟢 Done |
| R1 | feat: add CoinGecko provider for BTC (and other crypto) | 🟢 Done |
| R2 | feat: add remaining fiat currencies (PLN, DKK, CZK, HUF, NZD, KRW, THB, IDR, MYR, PHP, RON, ISK) | 🟢 Done |
| R3 | feat: multi-base conversion support | 🟢 Done |
| R4 | feat: generic offline queue with background retry | 🟢 Done |
| R5 | feat: self-host Frankfurter-compatible endpoint | 🟢 Done |
| L1 | feat: préstecs amb interessos — repayment desglossat capital + interès | 🟢 Done |
| L2 | feat: editar i esborrar moviments de préstec | 🟢 Done |
| L3 | UI: moviments de préstec en dues taules (Repayments / Interest) amb totals i línia de disbursement | 🟢 Done |
| A1 | docs: reflect current state in VISION.md | 🟢 Done |
| A2 | docs: add ARCHITECTURE.md (target structure + constitution) | 🟢 Done |
| A3 | docs: add MIGRATION.md (data playbook + storage inventory) | 🟢 Done |
| A4 | feat: Contracts project — shared DTOs API ↔ Web (end duplication) | 🟢 Done |
| A5 | test: golden datasets + persistence compatibility tests | ⬜ Pending (before next persistence change) |
| A6 | refactor: move Json\* repositories Core → Infrastructure | ⬜ Pending (only with a driver) |
| Q1 | feat: persist daily market quotes (lazy price cache + audit trail) | 🟢 Done |
| 197–200 | contracts + CI: DTOs únics a `MyAccountingApp.Contracts`, docker restore layer, gate de cobertura que exclo la UI del Web | 🟢 Done |
| 201–209 | refactor(web): split de totes les pàgines grans (DataQuality, Transactions, AssetTransactions, Import, Loans, Options, Settings, Portfolio, Summary) en components reutilitzables + tancament del diàleg de duplicats en resoldre'ls | 🟢 Done |
| 210, 212 | docs(architecture): splitting consolidat al roadmap; tema fosc i motiu de no-virtualització de taules registrats | 🟢 Done |
| 211 | feat(web): dark mode amb selector de tres estats | 🟢 Done |
| 213–219 | feat(web): Portfolio professional — capçalera amb KPIs i refresh únic, taules amb ColGroup i llegenda, colors de P&L visibles, retorn money-weighted i anualitzat, comptador de preus aïllat, gate local a `build/gate.sh` | 🟢 Done |
| 220–224 | fix(ibkr): parsers 2026 correctes, opcions fora del portfolio, tracker d'opcions posició a posició, preview de salut de dades i rebuild quirúrgic (provat i revertit — la base es deixa tal com està) | 🟢 Done |
| 225–227 | feat(web): opcions en dues pàgines reals (transaccions a Movements, posicions a Investments), delete per any a Settings i creació manual d'opcions | 🟢 Done |
| 228 | feat(web): export de les posicions del portfolio a CSV per importar a Investing.com | 🟢 Done |

## Llegenda

- 🟢 Done
- 🟡 In Progress
- ⬜ Pending

## Properes passes

- **A5 — Golden datasets + tests de compatibilitat de persistència**: fer-los abans del proper canvi de format de dades (p. ex. abans d'un driver SQL dins d'A6).
- **A6 — Moure els repositoris `Json*` de Core a Infrastructure**: només amb un driver concret que ho justifiqui.

## Pendents operatius (fora del codi)

- **Opcions IBKR 2026**: quan es descarregui el fitxer anual, crear-hi les 7 potes obertes que falten (totes ja expirades) i revisar-lo en sola lectura.
- **Vault bloquejat**: neteja del 401 sorollós (capturar-lo i deixar que el handler redirigeixi a `/unlock`) — acceptada, ajornada.
- **IBKR legacy 2022–2025**: replantejar l'execució del fix (deixar-ho com està i importar només 2026, o esborrar i re-importar des de Settings).
