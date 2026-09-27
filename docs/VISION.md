# MyAccountingApp – Vision & Guidelines

## General Goal

The project should evolve into a lightweight personal finance backend focused on:

* importing financial data from different brokers
* normalizing and validating that data
* storing it in a simple and maintainable format
* exposing the information through a simple API
* allowing future frontend/web integrations

In the first phase, the priority was NOT building a polished UI, but rather building a solid and extensible backend/data engine.

> **Status (2026-09):** the first phase is delivered. The project now ships a Blazor WASM frontend, options and loans support, portfolio analytics, split/reverse-split adjustments and import deduplication, while the data model has stayed **additive** and persistence remains simple encrypted JSON. The principles below still hold; today the focus is on data correctness, import robustness and incremental architecture work (see `docs/ARCHITECTURE.md` and `docs/MIGRATION.md`).

---

# Main Principles

## 1. Keep things simple

Avoid overengineering.

The project is a long-term pet project and should remain:

* easy to understand
* easy to refactor
* lightweight
* modular

Prefer pragmatic solutions over enterprise complexity.

---

## 2. Focus on the domain model first

The most important part of the application is the financial model and normalization pipeline.

Prioritize:

* transactions
* money and currencies
* conversions
* portfolio positions
* broker data normalization

The frontend was secondary during the first phase; today (Blazor WASM) it is a first-class surface and the features ship through it.

---

## 3. Use deterministic financial logic

Financial calculations should remain deterministic and traceable.

AI/LLMs may help with:

* parsing broker files
* extracting information
* categorizing raw data

But:

* balances
* FX calculations
* portfolio calculations
* accounting logic

should remain deterministic and implemented in code.

---

## 4. Keep infrastructure lightweight

Prefer:

* JSON persistence
* small local storage
* lightweight APIs
* simple file structures

Avoid introducing:

* large databases
* distributed systems
* microservices
* unnecessary infrastructure

unless truly needed later.

---

# Architecture Direction

The project should gradually evolve toward:

* Domain → business entities and rules
* Application → orchestration and use cases
* Infrastructure → repositories, APIs, persistence
* Presentation/API → HTTP endpoints

The domain layer should remain as independent as possible from infrastructure concerns.

---

# Priorities

> The original first-phase priorities below are delivered (see `docs/ROADMAP.md`). They remain the backbone every subsequent phase builds on.

## Priority 1 — Solid domain model

Improve and stabilize:

* transactions
* currencies
* FX handling
* money/value objects
* repository abstractions

---

## Priority 2 — Import pipeline

Create a flexible import flow:

Raw broker file
→ parsing
→ normalization
→ validation
→ domain entities
→ persistence

The normalization layer is one of the most important parts of the system.

---

## Priority 3 — Persistence

Use simple and transparent persistence mechanisms.

JSON storage is acceptable and even desirable during the first phase.

Prefer readable and debuggable formats.

---

## Priority 4 — API layer

Expose the backend through a simple API.

The API should allow:

* importing data
* querying transactions
* querying balances/positions
* querying conversions

Keep the API simple and maintainable.

---

# Important Non-Goals (for now)

Avoid prematurely implementing:

* complex authentication
* advanced frontend architecture
* event sourcing
* CQRS
* microservices
* heavy database infrastructure
* premature optimization

The goal is iteration speed and maintainability.

---

# Long-Term Direction (Future)

Already delivered (see `docs/ROADMAP.md`): web frontend (Blazor WASM), options support, dividend tracking, FX legs with pair identity, portfolio analytics (valuation, allocation, realized gains, cash-flow breakdowns), loans, data-quality tooling, split adjustments and import deduplication.

Still future:

* AI-assisted broker normalization and classification with confidence thresholds — the deterministic core never delegates financial calculations
* performance reporting: time-weighted / money-weighted returns, FX contribution
* derived-state consolidation: every derived report must be recalculable from persisted facts
* richer persistence (e.g. SQLite) behind the persistence abstraction — only if JSON stops being enough

These remain evolutions on top of a stable core, not immediate priorities.

---

# Final Philosophy

The project should behave more like:
"a lightweight financial data engine"

than:
"a traditional CRUD application".

The key value is transforming heterogeneous financial data into a clean, coherent, and extensible internal model.
