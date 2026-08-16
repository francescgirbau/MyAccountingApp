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

## Sessió 2026-08-16 — Fix FX (PR #154)

**Estat: COMPLETADA** — backfill fet sobre dades reals + PR https://github.com/francescgirbau/MyAccountingApp/pull/154

### Part 1 (operativa, feta)
- API llançada en Development (`dotnet run --project src/MyAccountingApp.Api --no-build --launch-profile MyAccountingApp.Api`, port 5000, vault desactivat, dades reals a src/MyAccountingApp.Api/data/, Frankfurter accessible des del sandbox).
- `POST /api/conversions/sync {"from":"2022-02-01","to":"2022-12-31"}` → 334 dies escrits a conversions.json.
- `/api/validate` abans/després: MISSING_FX 204 → 183 (els 21 de 2022 netejats; queden 2023(26)/2024(62)/2025(95)).

### Part 2 (codi, PR #154)
- `Application/Services/FxRateResolver.cs` (nou): exacte, sinó taxa anterior ≤ 5 dies naturals.
- `ValidationQuery.AddMissingFxRules` i `CurrencyRateService.FindFallback` ara usen el resolver (la conversió EUR va per GetFxQuotesAsync→GetConversionAsync→FindFallback).
- `ICurrencyRateService.SyncDatesAsync` (nou): agrupa dates consecutives → SyncRangeAsync per grup, atura si quota esgotada, retorna dies sincronitzats.
- `POST /api/data-quality/sync-missing-fx`: recull dates MISSING_FX del ValidateAll i les sincronitza.
- DataQuality.razor: botó "Fetch missing FX" només quan hi ha warnings MISSING_FX.
- Tests: +2 ValidationQuery (gap 1 dia no avisa / gap 7 avisa), +2 CurrencyRateService (fallback ≤5d, >5d throws, SyncDatesAsync), +1 ToEurConverter (>5d throws), +2 d'endpoint. Tests vells de stale actualitzats a dates dins dels 5 dies (2005→2023 ja no val).
- **Breaking canvi de comportament**: abans el fallback era il·limitat; ara >5 dies → ConversionNotAvailableException.
- 582 tests verds, -warnaserror, API+Web compilen.

### Apunts d'entorn
- La branca s'ha de crear des d'`origin/master` (mai de master local): master local tenia el commit del PR #153 no publ
- L'API en background: `setsid nohup dotnet run ... &` (el tool mata el process group al timeout); per aturar: `pkill -f MyAccountingApp`.
- `POST /api/transactions` (no asset-transactions) alimenta el repo de transaccions → warnings MISSING_FX.
- Un `Conversion` vell (2005-12-01 del FakeConversionRepository) ja NO serveix com a fallback en tests — inicialitzar amb `Initialize(new[]{new Conversion(fecha_recente,...)})`.

### Properes passes
1. Revisar manualment el botó "Fetch missing FX" a la UI de Data Quality (amb els 183 warnings restants ha d'aparèixer; clic → snackbar + Re-check).
2. Si el revisor ho demana: ajustos + push a la branca.
