# MyAccountingApp – First Phase Vision & Guidelines

## General Goal

The project should evolve into a lightweight personal finance backend focused on:

* importing financial data from different brokers
* normalizing and validating that data
* storing it in a simple and maintainable format
* exposing the information through a simple API
* allowing future frontend/web integrations

At this stage, the priority is NOT building a polished UI, but rather building a solid and extensible backend/data engine.

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

The frontend is secondary for now.

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

# First Phase Priorities

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

Potential future additions:

* AI-assisted broker normalization
* persistent memory/mappings
* portfolio analytics
* TWR calculations
* dividend tracking
* FX PnL separation
* options support
* web frontend

But these are future evolutions, not immediate priorities.

---

# Final Philosophy

The project should behave more like:
"a lightweight financial data engine"

than:
"a traditional CRUD application".

The key value is transforming heterogeneous financial data into a clean, coherent, and extensible internal model.
