# Market State Agent V1.2.0

Read-only cTrader indicator for classifying current market context.

## State model

The agent reports independent dimensions rather than one overloaded regime label:

- `Regime`: `RANGE | TRANSITION | TREND`
- `Direction`: `BEARISH | NEUTRAL | BULLISH`
- `ExpansionState`: `CONTRACTION | NEUTRAL | EXPANSION`
- `Volatility`: `LOW | NORMAL | HIGH`
- liquidity proxy, spread, session, freshness and confidence

`ExpansionState` is deliberately separate from volatility. Volatility describes the absolute movement level relative to its longer ATR baseline. Expansion/contraction describes whether recent realised movement is increasing or compressing relative to the immediately preceding local movement baseline.

## Dynamics estimator

The estimator uses true range from price bars:

1. Average true range over the most recent `Dynamics recent bars` (default `5`).
2. Average true range over the immediately preceding `Dynamics baseline bars` (default `20`).
3. `ExpansionRatio = recent / baseline`.

The classifier uses a Schmitt-trigger style hysteresis band:

- enter `CONTRACTION` at `<= 0.82x`;
- retain contraction while `<= 0.95x`;
- enter `EXPANSION` at `>= 1.20x`;
- retain expansion while `>= 1.05x`;
- otherwise report `NEUTRAL`.

This makes state changes materially harder than state retention and prevents rapid flips around `1.0x`.

`ExpansionConfidence` reports classification clarity. The evidence also contributes 10% to the existing structural confidence score.

## Safety boundary

This revision does **not** change the execution-intent mapping. `ExpansionState` is observation/context only. It does not place, modify, cancel or close orders and it does not grant any execution authority.

The existing V1 intent mapping remains:

- `TREND + BULLISH` -> `FOLLOW_TREND / BUY / STOP`
- `TREND + BEARISH` -> `FOLLOW_TREND / SELL / STOP`
- `RANGE` -> `TRADE_RANGE / BOTH / LIMIT`
- otherwise -> `HOLD`

A later strategy or portfolio decision may choose to consume the new dimension, but that is intentionally outside this change.

## Build

The project pins `cTrader.Automate` `1.0.17` and targets .NET 6. The repository workflow restores and builds the indicator and runs deterministic hysteresis tests.
