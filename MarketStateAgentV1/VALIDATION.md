# Validation

## Local structural validation

The implementation was checked for balanced C# delimiters and required state-contract elements. The deterministic hysteresis scenarios pass:

- neutral noise remains neutral;
- expansion enters only at the strong entry threshold;
- expansion survives pullback inside the hysteresis band;
- expansion exits below its retention threshold;
- contraction enters only at the strong entry threshold;
- contraction survives bounce inside the hysteresis band;
- contraction exits above its retention threshold;
- a sufficiently strong reversal may flip directly.

## Repository CI

`.github/workflows/validate-market-state.yml` restores and builds the cTrader indicator project and runs `tests/test_market_state_expansion_hysteresis.py` on the pull request.

## Scope limit

No live or demo order is required for this change because the indicator remains read-only and the execution-intent mapping is intentionally unchanged. Runtime market-data calibration of the initial thresholds is a later evidence task, not a prerequisite for the state-model implementation.
