# Installation

Preferred path: build the `MarketStateAgentV1.csproj` with the pinned `cTrader.Automate` package and import/use the resulting indicator in cTrader.

For an existing cTrader Algo project named `MarketStateAgentV1`, replace the complete indicator source with `MarketStateAgentV1.cs`, rebuild the indicator, and then rebuild any cBot that references it. The added `ExpansionOutput` is additive; the existing regime, direction, volatility, liquidity and bridge output names are preserved.

The execution intent contract remains unchanged in this revision.
