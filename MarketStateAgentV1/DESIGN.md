# Design notes

The dynamics axis answers a different question from the existing regime and volatility axes:

- regime: what structural mode is dominant?
- volatility: how large is movement versus a longer baseline?
- dynamics: is realised movement locally compressing, stable, or releasing?

The implementation therefore measures recent true range against a non-overlapping immediately preceding true-range baseline. It does not infer expansion from the existing ATR volatility label.

State retention uses hysteresis. This is intentional state memory rather than a per-tick threshold label: strong evidence is required to enter contraction/expansion, while weaker evidence is sufficient to retain it. Bar open time is used as the state-memory key so repeated recalculation of the same bar does not depend on a mutable output slot.

The initial thresholds are deterministic engineering defaults and should later be calibrated from replay/performance evidence per instrument/timeframe if the evidence justifies it.
