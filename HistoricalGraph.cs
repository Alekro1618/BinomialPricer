using Alpaca.Markets;

namespace BinomialPricer {
    public static class HistoricalGraph {
        public static async Task DrawPlot(IClock clock, SecretKey key, string symbol) {
             var data_client = Environments.Paper
                .GetAlpacaCryptoDataClient(key);
            var into = clock.TimestampUtc.AddMinutes(0);
            var from = into.AddDays(-1);
            var bars = await data_client.ListHistoricalBarsAsync(
                new HistoricalCryptoBarsRequest(symbol, from, into, BarTimeFrame.Hour)
            );

            var upper = new List<double>();
            var lower = new List<double>();
            var mean = new List<double>();
            var times = new List<DateTime>();

            foreach(var bar in bars.Items){
                (double up, double down, double exp) = await Program.GetTheOds(bar.TimeUtc, data_client, symbol);
                upper.Add((1+up) * (double)bar.Close);
                lower.Add((1+down) * (double)bar.Close);
                mean.Add((1+exp) * (double)bar.Close);
                times.Add(bar.TimeUtc);
            }

            var plot = new ScottPlot.Plot();
            var convbars = new List<ScottPlot.OHLC>();
            foreach(var bar in bars.Items) {
                convbars.Add(new ScottPlot.OHLC((double)bar.Open, (double)bar.High, (double)bar.Low, (double)bar.Close, bar.TimeUtc, TimeSpan.FromHours(1)));
            }


            plot.Add.Candlestick(convbars);
            plot.Add.Scatter(times, upper);
            plot.Add.Scatter(times, lower);
            plot.Add.Scatter(times, mean);
            plot.Axes.DateTimeTicksBottom();

            plot.SavePng("scatter.png",1200, 700);
        }
    }
}