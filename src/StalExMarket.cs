using System;

namespace ObenseuerOS
{
    internal sealed class StalExApi
    {
        internal Func<string> Status;
        internal Func<string> Indices;
        internal Func<string, string> List;
        internal Func<string, string> Quote;
        internal Func<string, double, string> Chart;
        internal Func<double, string> News;
        internal Func<string> Portfolio;
        internal Func<string, string, double, bool, string> Preview;
        internal Func<string, string, double, bool, string> Trade;
    }

    internal static class StalExMarket
    {
        private static readonly StalExApi api = new StalExApi
        {
            Status = MarketJson.Status,
            Indices = MarketJson.Indices,
            List = MarketJson.List,
            Quote = MarketJson.Quote,
            Chart = MarketJson.Chart,
            News = MarketJson.News,
            Portfolio = MarketJson.Portfolio,
            Preview = MarketJson.Preview,
            Trade = MarketJson.Trade
        };

        internal static StalExApi Get()
        {
            return api;
        }
    }
}
