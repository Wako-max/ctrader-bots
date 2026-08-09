using System;
using System.Collections.Generic;
using System.Text;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators
{
    public sealed class TosMarketStateResult
    {
        public string ContractVersion { get; internal set; }
        public string AgentVersion { get; internal set; }
        public string AnalysisIntent { get; internal set; }
        public string Symbol { get; internal set; }
        public string TimeFrame { get; internal set; }
        public DateTime EvaluatedAtUtc { get; internal set; }
        public DateTime SourceBarOpenTimeUtc { get; internal set; }
        public DateTime SourceBarCloseTimeUtc { get; internal set; }
        public DateTime ValidUntilUtc { get; internal set; }
        public bool IsUsable { get; internal set; }
        public bool MarketOpen { get; internal set; }
        public string Freshness { get; internal set; }
        public string Regime { get; internal set; }
        public string Direction { get; internal set; }
        public string ExpansionState { get; internal set; }
        public string Volatility { get; internal set; }
        public string Liquidity { get; internal set; }
        public string Spread { get; internal set; }
        public string Session { get; internal set; }
        public double Confidence { get; internal set; }
        public double ExpansionConfidence { get; internal set; }
        public double Adx { get; internal set; }
        public double DiPlus { get; internal set; }
        public double DiMinus { get; internal set; }
        public double AtrPips { get; internal set; }
        public double AtrRatio { get; internal set; }
        public double ExpansionRatio { get; internal set; }
        public double RelativeTickVolume { get; internal set; }
        public double SpreadPips { get; internal set; }
        public double SpreadToAtr { get; internal set; }
        public string Reason { get; internal set; }
    }

    public sealed class TosMarketStateIntent
    {
        public string ContractVersion { get; internal set; }
        public string IntentId { get; internal set; }
        public DateTime CreatedAtUtc { get; internal set; }
        public DateTime ValidUntilUtc { get; internal set; }
        public string Symbol { get; internal set; }
        public string TimeFrame { get; internal set; }
        public bool IsActionable { get; internal set; }
        public string Action { get; internal set; }
        public string Side { get; internal set; }
        public string OrderStyle { get; internal set; }
        public double Confidence { get; internal set; }
        public double AtrPips { get; internal set; }
        public string Reason { get; internal set; }
    }

    /// <summary>
    /// TOS Market State Agent V1.2.0.
    /// Expansion/contraction is an independent dynamics axis, not a replacement
    /// for RANGE/TREND and not an execution-authority signal.
    /// </summary>
    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class MarketStateAgentV1 : Indicator
    {
        public const string ContractVersion = "tos.market-state.v1";
        public const string AgentVersion = "1.2.0";
        public const string IntentContractVersion = "tos.market-state.execution-intent.v1";
        public const string AnalysisIntent = "CLASSIFY_CURRENT_MARKET_STATE";

        private const int SlopeLookback = 5;
        private const double RangeAdxMaximum = 18.0;
        private const double TrendAdxMinimum = 23.0;
        private const double TrendEmaSeparationAtrMinimum = 0.30;
        private const double TrendSlopeAtrMinimum = 0.025;
        private const double RangeEmaSeparationAtrMaximum = 0.60;
        private const double LowVolatilityRatio = 0.75;
        private const double HighVolatilityRatio = 1.30;
        private const double LowRelativeVolume = 0.65;
        private const double HighRelativeVolume = 1.35;
        private const double TightSpreadAtrFraction = 0.05;
        private const double WideSpreadAtrFraction = 0.15;
        private const double MaximumSpreadAtrFraction = 0.25;

        // Schmitt-trigger thresholds. Entry requires stronger evidence than
        // retention, preventing rapid state flips around 1.0x movement.
        private const double ContractionEnterRatio = 0.82;
        private const double ContractionExitRatio = 0.95;
        private const double ExpansionExitRatio = 1.05;
        private const double ExpansionEnterRatio = 1.20;

        private ExponentialMovingAverage _fastEma;
        private ExponentialMovingAverage _slowEma;
        private AverageTrueRange _atr;
        private DirectionalMovementSystem _dms;
        private int _minimumSourceIndex;
        private readonly Dictionary<DateTime, string> _expansionStateByBar = new Dictionary<DateTime, string>();

        [Parameter("Use last closed bar", DefaultValue = true)]
        public bool UseLastClosedBar { get; set; }

        [Parameter("Fast EMA", DefaultValue = 20, MinValue = 2, MaxValue = 200)]
        public int FastEmaPeriod { get; set; }

        [Parameter("Slow EMA", DefaultValue = 50, MinValue = 3, MaxValue = 500)]
        public int SlowEmaPeriod { get; set; }

        [Parameter("ADX period", DefaultValue = 14, MinValue = 3, MaxValue = 100)]
        public int AdxPeriod { get; set; }

        [Parameter("ATR period", DefaultValue = 14, MinValue = 3, MaxValue = 100)]
        public int AtrPeriod { get; set; }

        [Parameter("ATR baseline", DefaultValue = 50, MinValue = 10, MaxValue = 500)]
        public int AtrBaselinePeriod { get; set; }

        [Parameter("Tick-volume baseline", DefaultValue = 50, MinValue = 10, MaxValue = 500)]
        public int TickVolumeBaselinePeriod { get; set; }

        [Parameter("Dynamics recent bars", DefaultValue = 5, MinValue = 2, MaxValue = 50)]
        public int ExpansionRecentPeriod { get; set; }

        [Parameter("Dynamics baseline bars", DefaultValue = 20, MinValue = 5, MaxValue = 200)]
        public int ExpansionBaselinePeriod { get; set; }

        [Parameter("Freshness (bars)", DefaultValue = 2, MinValue = 1, MaxValue = 10)]
        public int FreshnessBars { get; set; }

        [Output("Confidence", LineColor = "#00D4AA", Thickness = 2)]
        public IndicatorDataSeries ConfidenceOutput { get; set; }

        [Output("Regime", LineColor = "#4F8CFF", Thickness = 1)]
        public IndicatorDataSeries RegimeOutput { get; set; }

        [Output("Direction", LineColor = "#F7C948", Thickness = 1)]
        public IndicatorDataSeries DirectionOutput { get; set; }

        [Output("Expansion state", LineColor = "#26A69A", Thickness = 1)]
        public IndicatorDataSeries ExpansionOutput { get; set; }

        [Output("Volatility", LineColor = "#B875FF", Thickness = 1)]
        public IndicatorDataSeries VolatilityOutput { get; set; }

        [Output("Liquidity proxy", LineColor = "#FF8A65", Thickness = 1)]
        public IndicatorDataSeries LiquidityOutput { get; set; }

        [Output("Bridge usable", LineColor = "#B0BEC5", Thickness = 1)]
        public IndicatorDataSeries BridgeUsableOutput { get; set; }

        [Output("Bridge ATR pips", LineColor = "#90A4AE", Thickness = 1)]
        public IndicatorDataSeries BridgeAtrPipsOutput { get; set; }

        [Output("Bridge spread/ATR", LineColor = "#78909C", Thickness = 1)]
        public IndicatorDataSeries BridgeSpreadToAtrOutput { get; set; }

        public TosMarketStateResult LatestState { get; private set; }
        public TosMarketStateIntent LatestIntent { get; private set; }

        protected override void Initialize()
        {
            _fastEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, FastEmaPeriod);
            _slowEma = Indicators.ExponentialMovingAverage(Bars.ClosePrices, SlowEmaPeriod);
            _atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);
            _dms = Indicators.DirectionalMovementSystem(AdxPeriod);

            int dynamicsHistory = ExpansionRecentPeriod + ExpansionBaselinePeriod + 1;
            _minimumSourceIndex = Math.Max(
                Math.Max(SlowEmaPeriod + SlopeLookback, dynamicsHistory),
                Math.Max(AtrPeriod + AtrBaselinePeriod, TickVolumeBaselinePeriod)) + 2;
        }

        public override void Calculate(int index)
        {
            SetUnknownOutputs(index);

            BarEvidence plottedEvidence = BuildBarEvidence(index);
            if (plottedEvidence.IsValid)
                WriteOutputs(index, plottedEvidence, plottedEvidence.VolumeQuality, plottedEvidence.StructuralConfidence);

            if (index != Bars.Count - 1)
                return;

            int sourceIndex = UseLastClosedBar && index > 0 ? index - 1 : index;
            BarEvidence currentEvidence = BuildBarEvidence(sourceIndex);
            LatestState = BuildCurrentState(sourceIndex, currentEvidence);
            LatestIntent = BuildExecutionIntent(LatestState);

            if (currentEvidence.IsValid)
            {
                double liveLiquidity = LatestState.IsUsable
                    ? ScoreLiquidity(currentEvidence.VolumeQuality, ScoreSpread(LatestState.SpreadToAtr))
                    : 0.0;

                WriteOutputs(index, currentEvidence, liveLiquidity, LatestState.Confidence / 100.0);
            }

            WriteBridgeOutputs(index, LatestState);
            DrawStatePanel(LatestState);
        }

        private BarEvidence BuildBarEvidence(int index)
        {
            if (index < _minimumSourceIndex || index >= Bars.Count)
                return BarEvidence.Invalid("INSUFFICIENT_HISTORY");

            double atr = _atr.Result[index];
            double atrAverage = AveragePrevious(_atr.Result, index, AtrBaselinePeriod);
            double averageTickVolume = AveragePrevious(Bars.TickVolumes, index, TickVolumeBaselinePeriod);
            double fast = _fastEma.Result[index];
            double slow = _slowEma.Result[index];
            double previousSlow = _slowEma.Result[index - SlopeLookback];
            double adx = _dms.ADX[index];
            double diPlus = _dms.DIPlus[index];
            double diMinus = _dms.DIMinus[index];
            double expansionRatio = CalculateExpansionRatio(index);

            if (!IsFinitePositive(atr) || !IsFinitePositive(atrAverage) ||
                !IsFinitePositive(averageTickVolume) || !IsFinite(fast) ||
                !IsFinite(slow) || !IsFinite(previousSlow) || !IsFinite(adx) ||
                !IsFinite(diPlus) || !IsFinite(diMinus) || !IsFinitePositive(expansionRatio) ||
                !IsFinitePositive(Symbol.PipSize))
                return BarEvidence.Invalid("REQUIRED_FEATURE_UNAVAILABLE");

            double atrPips = atr / Symbol.PipSize;
            double atrRatio = atr / atrAverage;
            double relativeTickVolume = Bars.TickVolumes[index] / averageTickVolume;
            double emaSeparationAtr = Math.Abs(fast - slow) / atr;
            double slowSlopeAtrPerBar = ((slow - previousSlow) / atr) / SlopeLookback;

            bool trendShape = emaSeparationAtr >= TrendEmaSeparationAtrMinimum &&
                              Math.Abs(slowSlopeAtrPerBar) >= TrendSlopeAtrMinimum;

            string regime;
            if (adx >= TrendAdxMinimum && trendShape)
                regime = "TREND";
            else if (adx <= RangeAdxMaximum && emaSeparationAtr <= RangeEmaSeparationAtrMaximum)
                regime = "RANGE";
            else
                regime = "TRANSITION";

            double emaSignal = Clamp((fast - slow) / (atr * 0.75), -1.0, 1.0);
            double diSignal = Clamp((diPlus - diMinus) / 25.0, -1.0, 1.0);
            double slopeSignal = Clamp(slowSlopeAtrPerBar / 0.10, -1.0, 1.0);
            double signedDirection = (0.40 * emaSignal) + (0.35 * diSignal) + (0.25 * slopeSignal);

            string direction;
            if (regime == "RANGE")
                direction = "NEUTRAL";
            else if (signedDirection >= 0.20)
                direction = "BULLISH";
            else if (signedDirection <= -0.20)
                direction = "BEARISH";
            else
                direction = "NEUTRAL";

            string expansionState = ClassifyExpansionState(index, expansionRatio);
            double expansionClarity = CalculateExpansionClarity(expansionState, expansionRatio);

            string volatility = atrRatio < LowVolatilityRatio
                ? "LOW"
                : atrRatio > HighVolatilityRatio ? "HIGH" : "NORMAL";

            string volumeState = relativeTickVolume < LowRelativeVolume
                ? "LOW"
                : relativeTickVolume > HighRelativeVolume ? "HIGH" : "NORMAL";

            double volumeQuality = Clamp((relativeTickVolume - 0.25) / 1.25, 0.0, 1.0);
            double structuralConfidence = CalculateStructuralConfidence(
                regime,
                adx,
                emaSeparationAtr,
                slowSlopeAtrPerBar,
                signedDirection,
                atrRatio,
                expansionClarity,
                volumeQuality);

            return new BarEvidence
            {
                IsValid = true,
                Reason = "OK",
                Regime = regime,
                Direction = direction,
                ExpansionState = expansionState,
                Volatility = volatility,
                VolumeState = volumeState,
                Adx = adx,
                DiPlus = diPlus,
                DiMinus = diMinus,
                AtrPips = atrPips,
                AtrRatio = atrRatio,
                ExpansionRatio = expansionRatio,
                ExpansionClarity = expansionClarity,
                RelativeTickVolume = relativeTickVolume,
                EmaSeparationAtr = emaSeparationAtr,
                SlowSlopeAtrPerBar = slowSlopeAtrPerBar,
                SignedDirection = signedDirection,
                VolumeQuality = volumeQuality,
                StructuralConfidence = structuralConfidence
            };
        }

        private TosMarketStateResult BuildCurrentState(int sourceIndex, BarEvidence evidence)
        {
            DateTime evaluatedAt = TimeInUtc;
            bool marketOpen = Symbol.MarketHours.IsOpened(evaluatedAt);
            string sessions = MarketSessions == MarketSession.None
                ? "NONE"
                : MarketSessions.ToString().ToUpperInvariant();

            if (!evidence.IsValid)
                return UnknownState(evaluatedAt, marketOpen, sessions, evidence.Reason, "UNKNOWN");

            TimeSpan barDuration = GetBarDuration(sourceIndex);
            if (barDuration <= TimeSpan.Zero)
                return UnknownState(evaluatedAt, marketOpen, sessions, "BAR_DURATION_UNAVAILABLE", "UNKNOWN");

            DateTime sourceOpen = Bars.OpenTimes[sourceIndex];
            DateTime sourceClose = sourceOpen.Add(barDuration);
            DateTime validUntil = sourceClose.Add(TimeSpan.FromTicks(barDuration.Ticks * (long)FreshnessBars));
            bool isFresh = evaluatedAt <= validUntil;

            double spreadPips = IsFinitePositive(Symbol.PipSize) &&
                                IsFinitePositive(Symbol.Ask) &&
                                IsFinitePositive(Symbol.Bid) &&
                                Symbol.Ask >= Symbol.Bid
                ? (Symbol.Ask - Symbol.Bid) / Symbol.PipSize
                : double.NaN;

            double spreadToAtr = IsFinite(spreadPips) && evidence.AtrPips > 0.0
                ? spreadPips / evidence.AtrPips
                : double.NaN;

            bool spreadAvailable = IsFinite(spreadPips) && IsFinite(spreadToAtr);
            string freshness = !marketOpen ? "MARKET_CLOSED" : isFresh ? "FRESH" : "STALE";
            string spreadState = spreadAvailable ? ClassifySpread(spreadToAtr) : "UNKNOWN";
            double spreadQuality = spreadAvailable ? ScoreSpread(spreadToAtr) : 0.0;
            double liquidityScore = spreadAvailable
                ? ScoreLiquidity(evidence.VolumeQuality, spreadQuality)
                : 0.0;
            string liquidity = spreadAvailable ? ClassifyLiquidity(liquidityScore) : "UNKNOWN";

            bool usable = marketOpen && isFresh && spreadAvailable;
            double confidence = usable
                ? 100.0 * Clamp(
                    (0.75 * evidence.StructuralConfidence) +
                    (0.15 * liquidityScore) +
                    (0.10 * spreadQuality),
                    0.0,
                    1.0)
                : 0.0;

            string reason;
            if (!marketOpen)
                reason = "MARKET_CLOSED";
            else if (!isFresh)
                reason = "SOURCE_STATE_EXPIRED";
            else if (!spreadAvailable)
                reason = "LIVE_SPREAD_UNAVAILABLE";
            else
                reason = "OK";

            return new TosMarketStateResult
            {
                ContractVersion = ContractVersion,
                AgentVersion = AgentVersion,
                AnalysisIntent = AnalysisIntent,
                Symbol = SymbolName,
                TimeFrame = Bars.TimeFrame.ToString(),
                EvaluatedAtUtc = evaluatedAt,
                SourceBarOpenTimeUtc = sourceOpen,
                SourceBarCloseTimeUtc = sourceClose,
                ValidUntilUtc = validUntil,
                IsUsable = usable,
                MarketOpen = marketOpen,
                Freshness = freshness,
                Regime = evidence.Regime,
                Direction = evidence.Direction,
                ExpansionState = evidence.ExpansionState,
                Volatility = evidence.Volatility,
                Liquidity = liquidity,
                Spread = spreadState,
                Session = sessions,
                Confidence = confidence,
                ExpansionConfidence = 100.0 * evidence.ExpansionClarity,
                Adx = evidence.Adx,
                DiPlus = evidence.DiPlus,
                DiMinus = evidence.DiMinus,
                AtrPips = evidence.AtrPips,
                AtrRatio = evidence.AtrRatio,
                ExpansionRatio = evidence.ExpansionRatio,
                RelativeTickVolume = evidence.RelativeTickVolume,
                SpreadPips = spreadAvailable ? spreadPips : double.NaN,
                SpreadToAtr = spreadAvailable ? spreadToAtr : double.NaN,
                Reason = reason
            };
        }

        private TosMarketStateIntent BuildExecutionIntent(TosMarketStateResult state)
        {
            string action = "HOLD";
            string side = "NONE";
            string orderStyle = "NONE";
            string reason = state == null ? "STATE_UNAVAILABLE" : state.Reason;
            bool actionable = false;

            // Intentionally unchanged from v1.1.1: dynamics are context only.
            if (state != null && state.IsUsable)
            {
                if (state.Regime == "TREND" && state.Direction == "BULLISH")
                {
                    action = "FOLLOW_TREND";
                    side = "BUY";
                    orderStyle = "STOP";
                    reason = "BULLISH_TREND";
                    actionable = true;
                }
                else if (state.Regime == "TREND" && state.Direction == "BEARISH")
                {
                    action = "FOLLOW_TREND";
                    side = "SELL";
                    orderStyle = "STOP";
                    reason = "BEARISH_TREND";
                    actionable = true;
                }
                else if (state.Regime == "RANGE")
                {
                    action = "TRADE_RANGE";
                    side = "BOTH";
                    orderStyle = "LIMIT";
                    reason = "TWO_SIDED_RANGE";
                    actionable = true;
                }
                else
                {
                    reason = state.Regime == "TRANSITION" ? "TRANSITION_STATE" : "NO_DIRECTIONAL_EDGE";
                }
            }

            DateTime sourceTime = state == null ? DateTime.MinValue : state.SourceBarOpenTimeUtc;
            string symbol = state == null ? SymbolName : state.Symbol;
            string timeFrame = state == null ? Bars.TimeFrame.ToString() : state.TimeFrame;
            string intentId = symbol + "|" + timeFrame + "|" + sourceTime.Ticks + "|" + action + "|" + side;

            return new TosMarketStateIntent
            {
                ContractVersion = IntentContractVersion,
                IntentId = intentId,
                CreatedAtUtc = state == null ? TimeInUtc : state.EvaluatedAtUtc,
                ValidUntilUtc = state == null ? DateTime.MinValue : state.ValidUntilUtc,
                Symbol = symbol,
                TimeFrame = timeFrame,
                IsActionable = actionable,
                Action = action,
                Side = side,
                OrderStyle = orderStyle,
                Confidence = state == null ? 0.0 : state.Confidence,
                AtrPips = state == null ? double.NaN : state.AtrPips,
                Reason = reason
            };
        }

        private TosMarketStateResult UnknownState(
            DateTime evaluatedAt,
            bool marketOpen,
            string sessions,
            string reason,
            string freshness)
        {
            return new TosMarketStateResult
            {
                ContractVersion = ContractVersion,
                AgentVersion = AgentVersion,
                AnalysisIntent = AnalysisIntent,
                Symbol = SymbolName,
                TimeFrame = Bars.TimeFrame.ToString(),
                EvaluatedAtUtc = evaluatedAt,
                SourceBarOpenTimeUtc = DateTime.MinValue,
                SourceBarCloseTimeUtc = DateTime.MinValue,
                ValidUntilUtc = DateTime.MinValue,
                IsUsable = false,
                MarketOpen = marketOpen,
                Freshness = freshness,
                Regime = "UNKNOWN",
                Direction = "UNKNOWN",
                ExpansionState = "UNKNOWN",
                Volatility = "UNKNOWN",
                Liquidity = "UNKNOWN",
                Spread = "UNKNOWN",
                Session = sessions,
                Confidence = 0.0,
                ExpansionConfidence = 0.0,
                Adx = double.NaN,
                DiPlus = double.NaN,
                DiMinus = double.NaN,
                AtrPips = double.NaN,
                AtrRatio = double.NaN,
                ExpansionRatio = double.NaN,
                RelativeTickVolume = double.NaN,
                SpreadPips = double.NaN,
                SpreadToAtr = double.NaN,
                Reason = reason
            };
        }

        private double CalculateStructuralConfidence(
            string regime,
            double adx,
            double emaSeparationAtr,
            double slowSlopeAtrPerBar,
            double signedDirection,
            double atrRatio,
            double expansionClarity,
            double volumeQuality)
        {
            double regimeClarity;
            if (regime == "TREND")
            {
                double adxStrength = Clamp((adx - RangeAdxMaximum) / (35.0 - RangeAdxMaximum), 0.0, 1.0);
                double shapeStrength = Clamp(
                    ((emaSeparationAtr / TrendEmaSeparationAtrMinimum) +
                     (Math.Abs(slowSlopeAtrPerBar) / TrendSlopeAtrMinimum)) / 4.0,
                    0.0,
                    1.0);
                regimeClarity = (0.65 * adxStrength) + (0.35 * shapeStrength);
            }
            else if (regime == "RANGE")
            {
                double adxWeakness = Clamp((TrendAdxMinimum - adx) / (TrendAdxMinimum - 8.0), 0.0, 1.0);
                double compression = 1.0 - Clamp(emaSeparationAtr / RangeEmaSeparationAtrMaximum, 0.0, 1.0);
                regimeClarity = (0.70 * adxWeakness) + (0.30 * compression);
            }
            else
            {
                regimeClarity = 0.40;
            }

            double directionClarity = regime == "RANGE"
                ? 1.0 - Math.Abs(signedDirection)
                : Math.Abs(signedDirection);
            directionClarity = Clamp(directionClarity, 0.0, 1.0);

            double volatilityClarity;
            if (atrRatio < LowVolatilityRatio)
                volatilityClarity = Clamp(0.50 + ((LowVolatilityRatio - atrRatio) / LowVolatilityRatio), 0.0, 1.0);
            else if (atrRatio > HighVolatilityRatio)
                volatilityClarity = Clamp(0.50 + ((atrRatio - HighVolatilityRatio) / HighVolatilityRatio), 0.0, 1.0);
            else
                volatilityClarity = Clamp(1.0 - (Math.Abs(atrRatio - 1.0) / 0.60), 0.0, 1.0);

            double confidence =
                (0.40 * regimeClarity) +
                (0.25 * directionClarity) +
                (0.15 * volatilityClarity) +
                (0.10 * Clamp(expansionClarity, 0.0, 1.0)) +
                (0.10 * volumeQuality);

            if (regime == "TRANSITION")
                confidence = Math.Min(confidence, 0.60);

            return Clamp(confidence, 0.0, 1.0);
        }

        private void WriteOutputs(int index, BarEvidence evidence, double liquidityScore, double confidenceScore)
        {
            ConfidenceOutput[index] = 100.0 * Clamp(confidenceScore, 0.0, 1.0);
            RegimeOutput[index] = evidence.Regime == "TREND" ? 100.0 : evidence.Regime == "RANGE" ? 0.0 : 50.0;
            DirectionOutput[index] = evidence.Direction == "BULLISH" ? 100.0 : evidence.Direction == "BEARISH" ? 0.0 : 50.0;
            ExpansionOutput[index] = evidence.ExpansionState == "EXPANSION" ? 100.0 : evidence.ExpansionState == "CONTRACTION" ? 0.0 : 50.0;
            VolatilityOutput[index] = evidence.Volatility == "HIGH" ? 100.0 : evidence.Volatility == "LOW" ? 0.0 : 50.0;
            LiquidityOutput[index] = 100.0 * Clamp(liquidityScore, 0.0, 1.0);
        }

        private void WriteBridgeOutputs(int index, TosMarketStateResult state)
        {
            if (state == null)
                return;

            BridgeUsableOutput[index] = state.IsUsable ? 1.0 : 0.0;
            BridgeAtrPipsOutput[index] = IsFinitePositive(state.AtrPips) ? state.AtrPips : double.NaN;
            BridgeSpreadToAtrOutput[index] = IsFinite(state.SpreadToAtr) ? state.SpreadToAtr : double.NaN;
        }

        private void SetUnknownOutputs(int index)
        {
            ConfidenceOutput[index] = double.NaN;
            RegimeOutput[index] = double.NaN;
            DirectionOutput[index] = double.NaN;
            ExpansionOutput[index] = double.NaN;
            VolatilityOutput[index] = double.NaN;
            LiquidityOutput[index] = double.NaN;
            BridgeUsableOutput[index] = double.NaN;
            BridgeAtrPipsOutput[index] = double.NaN;
            BridgeSpreadToAtrOutput[index] = double.NaN;
        }

        private void DrawStatePanel(TosMarketStateResult state)
        {
            if (Chart == null || state == null)
                return;

            string spreadText = IsFinite(state.SpreadPips)
                ? state.SpreadPips.ToString("F1") + " pips (" + state.Spread + ")"
                : "UNKNOWN";
            string sourceText = state.SourceBarCloseTimeUtc == DateTime.MinValue
                ? "UNKNOWN"
                : state.SourceBarCloseTimeUtc.ToString("yyyy-MM-dd HH:mm") + " UTC";
            string validUntilText = state.ValidUntilUtc == DateTime.MinValue
                ? "UNKNOWN"
                : state.ValidUntilUtc.ToString("yyyy-MM-dd HH:mm") + " UTC";

            StringBuilder text = new StringBuilder();
            text.AppendLine("TOS MARKET STATE AGENT v1.2.0");
            text.AppendLine(state.Symbol + " | " + state.TimeFrame + " | " + state.Session);
            text.AppendLine("Regime: " + state.Regime + " | Direction: " + state.Direction);
            text.AppendLine("Dynamics: " + state.ExpansionState + " | movement: " + FormatRatio(state.ExpansionRatio) + " | clarity: " + state.ExpansionConfidence.ToString("F0") + "%");
            text.AppendLine("Volatility: " + state.Volatility + " | ATR: " + FormatNumber(state.AtrPips, "F1") + " pips");
            text.AppendLine("Liquidity proxy: " + state.Liquidity + " | Tick volume: " + FormatRatio(state.RelativeTickVolume));
            text.AppendLine("Spread: " + spreadText);
            text.AppendLine("Confidence: " + state.Confidence.ToString("F0") + "% | " + state.Freshness);
            text.AppendLine("Source close: " + sourceText + " | Valid until: " + validUntilText);
            if (LatestIntent != null)
                text.AppendLine("Intent: " + LatestIntent.Action + " | " + LatestIntent.Side + " " + LatestIntent.OrderStyle);
            text.AppendLine("Status: " + state.Reason + " | READ-ONLY / NO EXECUTION");

            Chart.DrawStaticText(
                "TOS_MARKET_STATE_AGENT_V1_PANEL",
                text.ToString(),
                VerticalAlignment.Top,
                HorizontalAlignment.Right,
                Color.White);
        }

        private double CalculateExpansionRatio(int index)
        {
            int recentStart = index - ExpansionRecentPeriod + 1;
            int baselineEnd = recentStart - 1;
            int baselineStart = baselineEnd - ExpansionBaselinePeriod + 1;
            if (baselineStart < 1 || recentStart < 1)
                return double.NaN;

            double recentMovement = AverageTrueRangeFromBars(recentStart, index);
            double baselineMovement = AverageTrueRangeFromBars(baselineStart, baselineEnd);
            if (!IsFinitePositive(recentMovement) || !IsFinitePositive(baselineMovement))
                return double.NaN;

            return recentMovement / baselineMovement;
        }

        private double AverageTrueRangeFromBars(int startIndex, int endIndex)
        {
            if (startIndex < 1 || endIndex < startIndex || endIndex >= Bars.Count)
                return double.NaN;

            double sum = 0.0;
            int count = 0;
            for (int i = startIndex; i <= endIndex; i++)
            {
                double trueRange = TrueRangeAt(i);
                if (!IsFinitePositive(trueRange))
                    return double.NaN;
                sum += trueRange;
                count++;
            }

            return count > 0 ? sum / count : double.NaN;
        }

        private double TrueRangeAt(int index)
        {
            if (index < 1 || index >= Bars.Count)
                return double.NaN;

            double high = Bars.HighPrices[index];
            double low = Bars.LowPrices[index];
            double previousClose = Bars.ClosePrices[index - 1];
            if (!IsFinite(high) || !IsFinite(low) || !IsFinite(previousClose) || high < low)
                return double.NaN;

            double highLow = high - low;
            double highPreviousClose = Math.Abs(high - previousClose);
            double lowPreviousClose = Math.Abs(low - previousClose);
            return Math.Max(highLow, Math.Max(highPreviousClose, lowPreviousClose));
        }

        private string ClassifyExpansionState(int index, double ratio)
        {
            string previousState = "NEUTRAL";
            if (index > 0)
            {
                string retained;
                if (_expansionStateByBar.TryGetValue(Bars.OpenTimes[index - 1], out retained))
                    previousState = retained;
            }

            string state;
            if (previousState == "EXPANSION")
            {
                if (ratio >= ExpansionExitRatio)
                    state = "EXPANSION";
                else if (ratio <= ContractionEnterRatio)
                    state = "CONTRACTION";
                else
                    state = "NEUTRAL";
            }
            else if (previousState == "CONTRACTION")
            {
                if (ratio <= ContractionExitRatio)
                    state = "CONTRACTION";
                else if (ratio >= ExpansionEnterRatio)
                    state = "EXPANSION";
                else
                    state = "NEUTRAL";
            }
            else if (ratio >= ExpansionEnterRatio)
            {
                state = "EXPANSION";
            }
            else if (ratio <= ContractionEnterRatio)
            {
                state = "CONTRACTION";
            }
            else
            {
                state = "NEUTRAL";
            }

            _expansionStateByBar[Bars.OpenTimes[index]] = state;
            return state;
        }

        private double CalculateExpansionClarity(string state, double ratio)
        {
            if (!IsFinitePositive(ratio))
                return 0.0;

            if (state == "EXPANSION")
                return Clamp((ratio - ExpansionExitRatio) / (ExpansionEnterRatio - ExpansionExitRatio), 0.0, 1.0);

            if (state == "CONTRACTION")
                return Clamp((ContractionExitRatio - ratio) / (ContractionExitRatio - ContractionEnterRatio), 0.0, 1.0);

            double neutralHalfWidth = Math.Min(1.0 - ContractionEnterRatio, ExpansionEnterRatio - 1.0);
            return Clamp(1.0 - (Math.Abs(ratio - 1.0) / neutralHalfWidth), 0.0, 1.0);
        }

        private TimeSpan GetBarDuration(int index)
        {
            TimeSpan previous = index > 0 && index < Bars.Count
                ? Bars.OpenTimes[index] - Bars.OpenTimes[index - 1]
                : TimeSpan.Zero;
            TimeSpan next = index >= 0 && index + 1 < Bars.Count
                ? Bars.OpenTimes[index + 1] - Bars.OpenTimes[index]
                : TimeSpan.Zero;

            if (previous > TimeSpan.Zero && next > TimeSpan.Zero)
                return previous <= next ? previous : next;
            if (previous > TimeSpan.Zero)
                return previous;
            if (next > TimeSpan.Zero)
                return next;
            return TimeSpan.Zero;
        }

        private double AveragePrevious(DataSeries source, int index, int periods)
        {
            int start = index - periods;
            if (start < 0)
                return double.NaN;

            double sum = 0.0;
            for (int i = start; i < index; i++)
            {
                double value = source[i];
                if (!IsFinite(value))
                    return double.NaN;
                sum += value;
            }

            return sum / periods;
        }

        private string ClassifySpread(double spreadToAtr)
        {
            if (spreadToAtr <= TightSpreadAtrFraction)
                return "TIGHT";
            if (spreadToAtr >= WideSpreadAtrFraction)
                return "WIDE";
            return "NORMAL";
        }

        private double ScoreSpread(double spreadToAtr)
        {
            if (!IsFinite(spreadToAtr) || spreadToAtr < 0.0)
                return 0.0;
            return 1.0 - Clamp(spreadToAtr / MaximumSpreadAtrFraction, 0.0, 1.0);
        }

        private double ScoreLiquidity(double volumeQuality, double spreadQuality)
        {
            return Clamp((0.65 * volumeQuality) + (0.35 * spreadQuality), 0.0, 1.0);
        }

        private string ClassifyLiquidity(double score)
        {
            if (score < 0.40)
                return "LOW";
            if (score >= 0.70)
                return "HIGH";
            return "NORMAL";
        }

        private string FormatRatio(double value)
        {
            return IsFinite(value) ? value.ToString("F2") + "x" : "UNKNOWN";
        }

        private string FormatNumber(double value, string format)
        {
            return IsFinite(value) ? value.ToString(format) : "UNKNOWN";
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFinite(value) && value > 0.0;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (value < minimum)
                return minimum;
            if (value > maximum)
                return maximum;
            return value;
        }

        private sealed class BarEvidence
        {
            public bool IsValid { get; set; }
            public string Reason { get; set; }
            public string Regime { get; set; }
            public string Direction { get; set; }
            public string ExpansionState { get; set; }
            public string Volatility { get; set; }
            public string VolumeState { get; set; }
            public double Adx { get; set; }
            public double DiPlus { get; set; }
            public double DiMinus { get; set; }
            public double AtrPips { get; set; }
            public double AtrRatio { get; set; }
            public double ExpansionRatio { get; set; }
            public double ExpansionClarity { get; set; }
            public double RelativeTickVolume { get; set; }
            public double EmaSeparationAtr { get; set; }
            public double SlowSlopeAtrPerBar { get; set; }
            public double SignedDirection { get; set; }
            public double VolumeQuality { get; set; }
            public double StructuralConfidence { get; set; }

            public static BarEvidence Invalid(string reason)
            {
                return new BarEvidence
                {
                    IsValid = false,
                    Reason = reason,
                    Regime = "UNKNOWN",
                    Direction = "UNKNOWN",
                    ExpansionState = "UNKNOWN",
                    Volatility = "UNKNOWN",
                    VolumeState = "UNKNOWN"
                };
            }
        }
    }
}
