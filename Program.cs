using System.Data;
using Alpaca.Markets;


namespace BinomialPricer
{
    internal static class Program
    {
        private const String KEY_ID = "";
        private const String SECRET_KEY = "";

        public static async Task Main()
        {
            var key = new SecretKey(KEY_ID, SECRET_KEY);
            var client = Environments.Paper
                .GetAlpacaTradingClient(key);
            var symbol = "ETH/USD";
            var clock = await client.GetClockAsync();
            var data_client = Environments.Paper
                .GetAlpacaCryptoDataClient(key);
            var account = new AccountOrders(client);

            Backtest test = new Backtest(KEY_ID, SECRET_KEY);
            

            if (clock != null)
            {
                Console.WriteLine(
                    "Timestamp: {0}, NextOpen: {1}, NextClose: {2}",
                    clock.TimestampUtc, clock.NextOpenUtc, clock.NextCloseUtc);
            }

            for (int i = 0; i < 50; i++) {
                //Geting historical data
                var bars = await data_client.ListHistoricalBarsAsync(
                new HistoricalCryptoBarsRequest(symbol, clock.TimestampUtc.AddHours(-1), clock.TimestampUtc, BarTimeFrame.Hour));

                //Drawing Plot
                await HistoricalGraph.DrawPlot(clock, key, symbol);
                Console.WriteLine("Plot was updated");

                //Setting up Binomial Model with Ods
                var lastPrice = bars.Items[0].Close;
                (double up, double down, double exp) = await GetTheOds(clock.TimestampUtc.AddMinutes(-15), Environments.Paper
                .GetAlpacaCryptoDataClient(key), symbol);
                decimal currentPrice = lastPrice;
                BinModel model = new BinModel((double)currentPrice, up, down, Math.Abs(exp));

                double callOpt = Option.PriceByCRR(model, 36, (double)currentPrice * (1+exp), Option.CallPayoff);
                double putOpt = Option.PriceByCRR(model, 36, (double)currentPrice * (1+exp), Option.PutPayoff);
                double delta = callOpt - putOpt;

                //Making the decision
                if (delta > 0) {
                    Console.WriteLine("Best Option -> Call. Profit: {0}", callOpt);
                    if (!account.GetOrderStatus()) {
                        await account.MakeOrder(symbol, (decimal)0.01, currentPrice *(decimal)(1+exp), "BUY", 360);
                    }

                } else {
                    Console.WriteLine("Best Option -> Put. Profit: {0}", putOpt);
                    if (account.GetOrderStatus()) {
                        await account.MakeOrder(symbol, (decimal)0.01, currentPrice *(decimal)(1+exp), "SELL", 360);
                    }
                }

                //Waiting for price to change
                while(lastPrice == currentPrice) {
                    Console.WriteLine("Waiting for change of price. Current price: {0}", currentPrice);
                    Thread.Sleep(10000);

                    //Update data
                    clock = await client.GetClockAsync();
                    bars = await data_client.ListHistoricalBarsAsync(
                new HistoricalCryptoBarsRequest(symbol, clock.TimestampUtc.AddHours(-1), clock.TimestampUtc, BarTimeFrame.Hour));
                    if(bars.Items.Count == 0) {
                        continue;
                    }
                    currentPrice = bars.Items[0].Close;
                }

                //Calculating price change
                double firstPrice = (double)lastPrice;
                double secondPrice = (double)currentPrice;
                double change = (secondPrice - firstPrice) / firstPrice;
                Console.WriteLine("Price changed by {0}. Prediction was u: {1}, d: {2}, e: {3}", change, up, down, exp);
                
            }
        }

        public static void ChangeOrderState(ref bool isPut) {
            if (isPut) {
                isPut = false;
            } else {
                isPut = true;
            }
        }


        public static async Task<(double, double, double)> GetTheOds(DateTime dt, IAlpacaCryptoDataClient data_client, string symbol) {
            var into = dt;
            var from = into.AddDays(-1);
            var bars = await data_client.ListHistoricalBarsAsync(
                new HistoricalCryptoBarsRequest(symbol, from, into, BarTimeFrame.Hour)
            );
            int ups_count = 0;
            float ups_mean = 0;
            int downs_count = 0;
            float downs_mean = 0;
            foreach(var bar in bars.Items) {
                double height = (float)(bar.Close-bar.Open)/(float)bar.Open;
                if (height > 0) {
                    ups_count++;
                    ups_mean += (float)height;
                }
                if (height < 0) {
                    downs_count++;
                    downs_mean += (float)height;
                }
            }
            double expected = (ups_mean + downs_mean) / bars.Items.Count;
            ups_mean = ups_mean / ups_count;
            downs_mean = downs_mean / downs_count;
            
            return (ups_mean, downs_mean, expected);
        }
    }
}

