using System.Linq.Expressions;
using Alpaca.Markets;
using Microsoft.VisualBasic;
using ScottPlot;

namespace BinomialPricer {
    class Backtest {

        float account = 100000;
        float earnings = 0;
        public float current_price = 0;
        public float asset_price = 0;
        int i = 0;
        IPage<IBar> bars;
        IAlpacaCryptoDataClient data_client;
        IAlpacaTradingClient client;
        IClock clock;
        string symbol = "BTC/USD";
        int exp = 0;
        string status = "waiting";
        public string GetStatus() => status;
        float sp;

        public Backtest(String KEY_ID, String SECRET_KEY) {
            var key = new SecretKey(KEY_ID, SECRET_KEY);
            client = Environments.Paper
                .GetAlpacaTradingClient(key);
            data_client = Environments.Paper
                .GetAlpacaCryptoDataClient(key);
        }

        public async void GetClientClock() {
            clock = await client.GetClockAsync();
        }

        public async void LoadHistBars() {
            Console.WriteLine("TEST: Loading bars");
            bars = await data_client.ListHistoricalBarsAsync(
                new HistoricalCryptoBarsRequest(symbol, clock.TimestampUtc.AddMonths(-6), clock.TimestampUtc, BarTimeFrame.Hour));
            Console.WriteLine("TEST: Loaded bars");
        }

        public void GetNextPrice() {
            i++;
            Console.WriteLine("TEST: Next price");
            if (exp > 0) {
                if (exp == 1) {
                    Console.WriteLine("TEST: Order has expired");
                    status = "waiting";
                }
                if (status == "buying" && current_price < sp) {
                    Console.WriteLine("TEST: Change status to filled");
                    status = "filled";
                    account -= current_price;
                    asset_price = current_price;
                }
                if (status == "selling" && current_price > sp) {
                    Console.WriteLine("TEST: Change status to waiting");
                    status = "waiting";
                    account += current_price;
                    AddEarnings(current_price - asset_price);
                    asset_price = 0;
                }
                exp--;
            }
            current_price = (float)bars.Items[i].Open;
        }

        public void AddEarnings(float s) {
            Console.WriteLine("TEST: Earnings: {0}", s);
            Console.WriteLine("TEST: Whole earnings: {0}", earnings);
            earnings += s;
        }

        public void MakeOrder(float strikePrice, string type, int expTime) {
            exp = expTime;
            if (type == "BUY") {
                status = "buying";
                Console.WriteLine("TEST: Call option with strike price: {0}", strikePrice);
            } else {
                status = "selling";
                Console.WriteLine("TEST: Put option with strike price: {0}", strikePrice);
            }
        }
    }
}