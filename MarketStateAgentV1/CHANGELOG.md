# Changelog

## 1.2.0 — 2026-08-09

- Added independent `CONTRACTION | NEUTRAL | EXPANSION` market-dynamics state.
- Added `ExpansionRatio` evidence based on recent true range versus the immediately preceding baseline.
- Added `ExpansionConfidence` classification clarity.
- Added Schmitt-trigger hysteresis to prevent rapid state oscillation.
- Added public numeric `ExpansionOutput` for read-only downstream consumption.
- Kept the existing execution-intent mapping unchanged; dynamics remain observational context only.
- Added deterministic hysteresis tests and CI build validation.
