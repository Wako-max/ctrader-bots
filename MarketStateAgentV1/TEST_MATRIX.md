# Test matrix

| Case | Expected |
| --- | --- |
| ratios near 1.0 | remain `NEUTRAL` |
| neutral at 1.20+ | enter `EXPANSION` |
| expansion at 1.05+ | retain `EXPANSION` |
| expansion below 1.05 | leave expansion |
| neutral at 0.82- | enter `CONTRACTION` |
| contraction at 0.95- | retain `CONTRACTION` |
| contraction above 0.95 | leave contraction |
| strong expansion-to-contraction reversal | direct flip permitted |
| missing evidence | `UNKNOWN` / unusable evidence path |
| new dynamics state | does not alter V1 execution-intent mapping |
