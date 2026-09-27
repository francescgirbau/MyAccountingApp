# MyAccountingApp — Data & Migration Playbook

How to evolve the codebase without ever risking the real data.
Companion to `docs/ARCHITECTURE.md` (target & rules) and `docs/VISION.md` (why).

---

## The one rule

> **Any version of the app must be able to load the data written by any previous version.**

Everything below serves that rule.

---

## Storage inventory (verified 2026-09)

**Location & encryption**

- User data lives in `data/` (mounted at `/app/data` in Docker). **Never touch live files directly** — the user keeps their own copies and only edits through the app (or explicit, approved one-off scripts).
- Files are encrypted at rest by `VaultService` (AES-GCM, key derived from the passphrase, unlock verifier) via `EncryptedJsonFileStorage`. `DisabledVaultService` is used when the vault is off (local/dev).
- Repositories are **whole-document rewrites**: `AddOrUpdate`/`Delete` read all rows, modify, rewrite the full file. There is no incremental/append path.

**Files** (`data/`), backed by `src/MyAccountingApp.Core/Persistence/Repositories/`:

| File | Repository | Notes |
|---|---|---|
| `transactions.json` | `CompositeTransactionRepository` (memory cache + JSON) | Also: `JsonTransactionRepository` |
| `portfolio.json` | `CompositePortfolioRepository` | Asset lots |
| `options.json` | `JsonOptionTransactionRepository` | |
| `loans.json` | `JsonLoanRepository` | |
| `loan-movements.json` | `JsonLoanMovementRepository` | |
| `conversions.json` | `CompositeConversionRepository` | FX quote cache, rebuildable |
| `pending_conversions.json` | `JsonPendingWorkRepository` | Offline/retry queue |
| `api_quota.json` | `JsonApiQuotaRepository` | Provider quota counter |
| `market_quotes.json` | `JsonMarketQuoteRepository` | Daily market prices (lazy cache: one per symbol+day, auditable, rebuilt on fetch) |

**Composite repositories** keep an in-memory copy as the read path and the JSON as the write path.
When the vault is locked at startup the memory cache stays empty until unlock → `Reload()`.

**Serialization**

- `System.Text.Json`, `PropertyNameCaseInsensitive`, `JsonStringEnumConverter` (enums serialized as strings), `WriteIndented`.
- Adding a new optional property or a new enum member is **backward compatible** — the preferred way to evolve.

**Implicit healing today (catalogued, keep or change consciously)**

- `JsonTransactionRepository.GetAll()` deduplicates by `Transaction.Id` on load (keeps the last occurrence) and rewrites the file when it finds duplicates.
- `JsonTransactionRepository` and `JsonPortfolioRepository` repair truncated JSON on parse failure (keep last `}` + close the array) and write the recovered content back.
- `JsonMarketQuoteRepository.GetAll()` deduplicates by symbol + day (keeps the newest) and repairs truncated JSON on parse failure.

---

## Schema evolution

**Additive changes (preferred, no migration needed):** new optional properties, new enum members,
new entities/files. Verified by the compatibility tests below.

**No JSON version envelope for now.** Files are encrypted, whole-document rewrites; a
`"version": 1` field adds risk to every write path without payoff. Introduce **explicit
versioning only when a breaking shape change is unavoidable**, and then:

**Breaking change recipe (rare):**

```text
old shape --on load--> detect old shape --> project to new shape --> domain --> save (new shape)
```

- Migration is **lazy and write-through**: the old file is only rewritten once it validates and round-trips.
- Ship the old-shape sample + a test that proves `old JSON → load → save → load again` preserves semantics.
- Never "migrate and trash": keep a backup copy (vault `/backup` endpoint or the user's own copy) before the first write of the new shape.

---

## Golden datasets & compatibility tests

**Inputs:** synthetic/sanitized samples — **never personal encrypted data in the repo**.
One sample per repository, covering real cases:

- plain transactions, FX legs, dividends + withholding tax
- corporate actions (imported as Buy/Sell, per the IBKR importer)
- split / reverse-split rows (post #192: `Quantity` changed, `Amount` preserved)
- options, loans + movements, multi-currency, fractional quantities (funds)
- optional fields present/absent, older enum values (e.g. pre-extension fiat list), truncated-file repair cases

**Test shape (per repository):**

```text
legacy JSON  →  load  →  domain  →  save  →  load again  →  same semantic data
```

**Rule:** every persistence refactor ships these tests (Constitution rule 5).

---

## Recipe: moving a repository (Core → Infrastructure, when there is a driver)

One repository per MR, refactor-only:

1. Move the class, change the namespace.
2. Update DI registration.
3. Compatibility tests green.
4. Full suite green (Debug **and** Release with `-warnaserror --no-incremental`).
5. Verify the live JSON still loads (read-only check; never write on a refactor).
6. Delete the old class **last**.

The MR must contain no behavior change.

---

## What we are NOT doing

- No single "big bang" migration of all data into a new format.
- No file renames/reformats (breaks the user's backups and tooling).
- No SQLite until JSON stops being enough (see VISION/ARCHITECTURE).
- No AI inside deterministic financial paths.
- No unapproved data modification: **every change to real data needs explicit user OK**
  (imports/resets/one-off scripts included).

---

## Sequencing

1. **Live-data QA first** (blocking any persistence refactor): CEQ consolidation, dedup re-import, GBP normalization validated on the running app.
2. IBKR multi-year import (#23).
3. Then incremental architecture work: `Contracts` project, golden datasets, and — only with a driver — the `Core` → `Infrastructure` split.