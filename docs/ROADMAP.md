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
| 48 | feat: currency conversions stabilization (resilience, gap sync, status endpoint) | 🟡 In Progress |
| R1 | feat: add CoinGecko provider for BTC (and other crypto) | ⬜ Pending |
| R2 | feat: add remaining fiat currencies (PLN, DKK, CZK, HUF, NZD, KRW, THB, IDR, MYR, PHP, RON, ISK) | ⬜ Pending |
| R3 | feat: multi-base conversion support | ⬜ Pending |
| R4 | feat: generic offline queue with background retry | ⬜ Pending |
| R5 | feat: self-host Frankfurter-compatible endpoint | ⬜ Pending |

## Llegenda

- 🟢 Done
- 🟡 In Progress
- ⬜ Pending

## Roadmap (R1–R5)

- **R1 — CoinGecko per BTC**: afegir proveïdor de cripto per cobrir BTC amb el seu propi proveïdor i base, sense contaminar les sèries fiat.
- **R2 — Monedes fiat addicionals**: estendre l'enum amb les monedes que falta de Frankfurter (PLN, DKK, CZK, HUF, NZD, KRW, THB, IDR, MYR, PHP, RON, ISK); canvi trivial.
- **R3 — Multi-base**: permetre consultar conversions entre qualssevol monedes, no només des d'EUR.
- **R4 — Cua offline genèrica**: generalitzar la cua de pendents amb retry de fons automàtic.
- **R5 — Self-host Frankfurter**: allotjar un endpoint compatible amb Frankfurter per no dependre del servei públic.
