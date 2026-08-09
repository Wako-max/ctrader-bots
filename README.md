# cTrader Bots

Repository for cTrader automation components and supporting validation.

## Market State Agent

`MarketStateAgentV1/` contains the read-only Market State Agent implementation. Version 1.2.0 adds contraction/expansion as an independent market-dynamics dimension with deterministic hysteresis, while preserving the existing execution-intent mapping and authority boundaries.

See:

- `MarketStateAgentV1/README.md`
- `MARKET_STATE_CONTRACTION_EXPANSION.md`
- `MarketStateAgentV1/VALIDATION.md`
