---

**Title:** Parse CSV directly instead of relying on LLM

**Description:**

Current approach relies on LLM to parse CSV which is:
- Slow (LLM processes entire file)
- Error-prone (LLM makes mistakes with complex files)
- Expensive (high token usage)

**Proposed solution:**

1. Parse CSV directly using CsvHelper or similar
2. Use LLM only for classification:
   - Classify transaction type (dividend, fee, deposit, trade)
   - Clean/normalize data when needed

**Benefits:**
- Faster parsing
- More reliable
- Lower cost
- Deterministic output

**Implementation:**

1. Add CSV parsing library (CsvHelper)
2. Create IBKR CSV model (TransactionRecord)
3. Parse CSV directly to list of records
4. Use LLM only to classify each record
5. Map to domain objects

---
