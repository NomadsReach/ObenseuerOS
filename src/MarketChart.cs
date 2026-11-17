using System;

namespace ObenseuerOS
{
    internal struct MarketChartPoint
    {
        internal readonly float X, Y;
        internal MarketChartPoint(float x, float y) { X = x; Y = y; }
    }

    internal static class MarketChart
    {
        internal static MarketChartPoint[] Points(double[] values)
        {
            MarketChartPoint[] result = new MarketChartPoint[values.Length];
            if (values.Length == 0) return result;
            double low = values[0], high = values[0];
            foreach (double value in values) { low = Math.Min(low, value); high = Math.Max(high, value); }
            for (int i = 0; i < values.Length; i++)
                result[i] = new MarketChartPoint(values.Length == 1 ? 1f : (float)i / (values.Length - 1), high == low ? 0.5f : (float)(1.0 - (values[i] - low) / (high - low)));
            return result;
        }
    }
}
