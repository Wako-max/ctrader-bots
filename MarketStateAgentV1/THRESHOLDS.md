# Initial dynamics thresholds

| Transition | Ratio |
| --- | ---: |
| Neutral -> Contraction | `<= 0.82` |
| Retain Contraction | `<= 0.95` |
| Neutral -> Expansion | `>= 1.20` |
| Retain Expansion | `>= 1.05` |

These values create an intentional dead band. They are initial deterministic thresholds for the first implementation, not strategy alpha parameters and not universal constants. Any later per-symbol/timeframe calibration must be evidence-backed and must preserve the independent dynamics-axis semantics.
