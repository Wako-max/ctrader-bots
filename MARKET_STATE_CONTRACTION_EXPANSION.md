# Architectural decision — Market State contraction / expansion

## Decision

The Market State Agent models contraction/expansion as an independent market-state dimension alongside regime, direction, volatility, liquidity, spread, session and confidence.

The canonical states are:

- `CONTRACTION`
- `NEUTRAL`
- `EXPANSION`

This dimension does not replace `RANGE | TRANSITION | TREND`.

## Meaning

- Regime describes dominant market structure.
- Volatility describes the absolute realised movement level relative to a longer ATR baseline.
- Expansion/contraction describes the local trajectory of realised movement: whether the newest movement window is compressing or releasing versus the immediately preceding baseline window.

Therefore combinations such as `RANGE + CONTRACTION`, `RANGE + EXPANSION`, `TREND + CONTRACTION`, and `TREND + EXPANSION` are all valid.

## Estimator

The first implementation compares mean true range over 5 recent bars with mean true range over the preceding 20 bars. The ratio is explicit evidence in `ExpansionRatio`.

Hysteresis is mandatory:

- enter contraction at `<= 0.82x`, retain until `> 0.95x`;
- enter expansion at `>= 1.20x`, retain until `< 1.05x`;
- otherwise neutral.

The thresholds are initial deterministic operating values, not a claim of universal market optimality. They can later be calibrated from replay/performance evidence without changing the state model.

## Authority boundary

The new state is observational context. This change does not modify the existing execution-intent mapping and does not grant broker, risk, portfolio, or execution authority to the Market State Agent.
