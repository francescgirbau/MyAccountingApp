# Scratchpad — MyAccountingApp

## Darrera tasca: Allocation Overview (PR #153)

**Estat: COMPLETADA (pendent de revisió humana i manual checks)**

### Què s'ha fet
Ticket "portfolio allocation overview" implementat a `feat/portfolio-allocation-overview` → PR https://github.com/francescgirbau/MyAccountingApp/pull/153

Fitxers nous:
- `src/MyAccountingApp.Application/DTOs/PortfolioOverviewDto.cs` — records `PortfolioOverviewDto`, `PortfolioPositionRowDto`, `AllocationSliceDto` (CostEur nullable; row per posició oberta)
- `src/MyAccountingApp.Application/Interfaces/IPortfolioOverviewQuery.cs`
- `src/MyAccountingApp.Application/Services/PortfolioOverviewQuery.cs` — FIFO (FifoCalculator), preu via `GetLastQuoteAsync`/`GetCachedPriceAsync` (isStale si no hi ha preu fresc), conversió EUR via `IConversionRepository.GetLatestOnOrBefore` (rate = moneda per EUR → amount/rate). Posicions no priced NI convertibles excloses de totals i pesos. Top-8 slices + "Other". `WorkingRow` intern mutable (StyleCop d'aquest repo NO admet `with` expressions — SA1101 fals positiu, i SA1407/SA1119 estrictes).
- `tests/MyAccountingApp.Application.Tests/Services/PortfolioOverviewQueryTests.cs` — 9 tests (70/30→50/50, unpriced, closed, cost 0, cap Other, suma pesos=1, mateixes keys, USD→EUR, count options)

Fitxers modificats:
- `DependencyInjection.cs` — registre `IPortfolioOverviewQuery`
- `Endpoints/PortfolioEndpoints.cs` — `GET /api/portfolio/overview` (registrat ABANS de `{symbol}` per no col·lisionar)
- `Web/Models/Dtos.cs` — mirror classes web dels DTOs
- `Web/Pages/Portfolio.razor` — cards KPI (market value, invested, P/L+%), alerts (market closed / unpriced / options), 2 donuts SVG (220px, paleta de 10 colors per hash de ticker, "Other" gris #9E9E9E; click → moviments), llegenda amb Δ wt i P/L %, taula sortable (SortBy/SortedRows amb nullable decimals, nulls al final)
- `tests/MyAccountingApp.Api.Tests/PortfolioEndpointsTests.cs` — 1 test endpoint (unpriced)

### Decisions de disseny
- Pesos amb decimals sense arrodonir (sumen 1.0 exacte); valors arrodonits a 2.
- KPI "invested" = Σ CostEur de posicions valorades (priced + convertibles) — consistent amb els pies.
- Posició cost 0: no pes de compra, però sí a la llista/denominador actual (CurrentWeight = MV/marketEur).
- Options: comptades com a `OptionSymbolCount` i fora dels pies (nota a la UI).
- Test count total: 575 passing (45 Domain + 181 Application + 273 Core + 76 Api).

### Entorn (important)
- `dotnet` NO està al PATH: `export PATH="$HOME/.dotnet:$PATH"` abans de qualsevol dotnet.
- Commands: `dotnet build <proj> -warnaserror` / `dotnet test -warnaserror`.
- `master` està PROTEGIT: mai push directe → branca + PR via `"/mnt/c/Program Files/GitHub CLI/gh.exe" pr create --base master`.
- Estil commits: `feat(area): desc` amb #num PR.
- Fakes de TestUtilities: `FakeMarketPriceService` (prices + staleSymbols), `FakeConversionRepository` (default USD 1.1 a 2025-01-01).

### Properes passes (per la propera sessió)
1. **Manual checks** de la llista del PR (UI: cards, pies, click, refresh, alerts) — executar l'app i provar al navegador.
2. Si hi ha feedback del revisor: ajustar i force-push a la branca (no amend de commits ja revisats si el repo ho prefereix — fer servir nous commits o amend amb `--no-edit`).
3. Quan s'accepti: merge (el flux actual crea PR → merge per GitHub).
