# State contract

`ExpansionState` values are uppercase machine-readable strings:

- `CONTRACTION`
- `NEUTRAL`
- `EXPANSION`
- `UNKNOWN` only when required evidence is unavailable

`ExpansionRatio` is the positive recent-to-baseline true-range ratio. `ExpansionConfidence` is a 0–100 classification-clarity score.

The public numeric `ExpansionOutput` maps states to:

- contraction: `0`
- neutral: `50`
- expansion: `100`
- unavailable: `NaN`

These values are context only. They do not change execution permissions or bypass any downstream safety boundary.
