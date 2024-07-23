using System.Data;
using Alpaca.Markets;


namespace BinomialPricer
{
    internal static class Program
    {
        private const String KEY_ID = "PK7KKSAXK51R1LGHAXBJ";
        private const String SECRET_KEY = "gpwUAVHxBdJn4ZuYVHCfNmKoKKwB6QnvPnIXnZeu";

        public static async Task Main()
        {
            var key = new SecretKey(KEY_ID, SECRET_KEY);
            var client = Environments.Paper
                .GetAlpacaTradingClient(key);
            var symbol = "BTC/USD";
            var clock = await client.GetClockAsync();
            var data_client = Environments.Paper
                .GetAlpacaCryptoDataClient(key);

            if (clock != null)
            {
                Console.WriteLine(
                    "Timestamp: {0}, NextOpen: {1}, NextClose: {2}",
                    clock.TimestampUtc, clock.NextOpenUtc, clock.NextCloseUtc);
            }


            bool isOrderPut = false;
            for (int i = 0; i < 50; i++) {
                //Geting historical data
                var bars = await data_client.ListHistoricalBarsAsync(
                new HistoricalCryptoBarsRequest(symbol, clock.TimestampUtc.AddHours(-1), clock.TimestampUtc, BarTimeFrame.Hour));

                //Drawing Plot
                await DrawPlot(clock, key, symbol);
                Console.WriteLine("Plot was updated");

                //Setting up Binomial Model with Ods
                var lastPrice = bars.Items[0].Close;
                (double up, double down, double exp) = await GetTheOds(clock.TimestampUtc.AddMinutes(-15), Environments.Paper
                .GetAlpacaCryptoDataClient(key), symbol);
                decimal currentPrice = lastPrice;
                BinModel model = new BinModel((double)currentPrice, up, down, Math.Abs(exp));

                double callOpt = Option.PriceByCRR(model, 10, (double)currentPrice * (1+exp), Option.CallPayoff);
                double putOpt = Option.PriceByCRR(model, 10, (double)currentPrice * (1+exp), Option.PutPayoff);
                double delta = callOpt - putOpt;

                //Making the decision
                if (delta > 0) {
                    Console.WriteLine("Best Option -> Call. Profit: {0}", callOpt);
                    if (isOrderPut == false) {
                        isOrderPut = true;
                        await MakeOrder(client, symbol, 1, currentPrice *(decimal)(1+exp), "BUY");
                    }

                } else {
                    Console.WriteLine("Best Option -> Put. Profit: {0}", putOpt);
                    if (isOrderPut == true) {
                        isOrderPut = false;
                        await MakeOrder(client, symbol, 1, currentPrice *(decimal)(1+exp), "SELL");
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

        public static async Task MakeOrder(IAlpacaTradingClient client, string symbol, decimal qty, decimal limit, string type) {
            IOrder order;
            bool ordered = false;
            for(int j = 0; j < 10; j++) {
                try {
                    if (type == "BUY") {
                        order = await client.PostOrderAsync(LimitOrder.Buy(symbol.Replace("/", string.Empty), OrderQuantity.Fractional(qty), limit).WithDuration(TimeInForce.Gtc));
                        //while(order.OrderStatus != OrderStatus.Filled) {
                        //    Console.WriteLine("Waiting for order to be filled...");
                        //    Thread.Sleep(10000);
                        //}
                        Console.WriteLine("Order was filled!");
                        break;
                    } else {
                        order = await client.PostOrderAsync(LimitOrder.Sell(symbol.Replace("/", string.Empty), OrderQuantity.Fractional(qty), limit).WithDuration(TimeInForce.Gtc));
                        //while(order.OrderStatus != OrderStatus.Filled) {
                        //    Console.WriteLine("Waiting for order to be filled...");
                        //    Thread.Sleep(10000);
                        //}
                        Console.WriteLine("Order was filled!");
                        break;
                    }
                    ordered = true;
                    
                } catch {
                    Console.WriteLine("Error. Trying to to make order again");
                    continue;
                }
            }
            if (ordered == false) {
                throw new TaskCanceledException();
            }
        }

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
                (double up, double down, double exp) = await GetTheOds(bar.TimeUtc, data_client, symbol);
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

