using Alpaca.Markets;

namespace BinomialPricer {
    public class AccountOrders {
        private IAlpacaTradingClient client;
        private bool isOrderPut = false;
        public AccountOrders(IAlpacaTradingClient client) {
            this.client = client;
        }

        public async Task MakeOrder(string symbol, decimal qty, decimal limit, string type, int expiration) {
            IOrder order;
            bool ordered = false;
            for(int j = 0; j < 10; j++) {
                try {
                    if (type == "BUY") {
                        order = await client.PostOrderAsync(LimitOrder.Buy(symbol.Replace("/", string.Empty), OrderQuantity.Fractional(qty), limit).WithDuration(TimeInForce.Gtc));
                    } else {
                        order = await client.PostOrderAsync(LimitOrder.Sell(symbol.Replace("/", string.Empty), OrderQuantity.Fractional(qty), limit).WithDuration(TimeInForce.Gtc));    
                    }
                    ChangeOrderStatus();
                    Console.WriteLine("Order was accepted!");
                    ordered = true;
                    WaitToExpire(order, client, expiration);
                    break;
                    
                } catch {
                    Console.WriteLine("Error. Trying to to make order again");
                    continue;
                }
            }
            if (ordered == false) {
                throw new TaskCanceledException();
            }
        }

        public void ChangeOrderStatus() {
            if(isOrderPut) {
                isOrderPut = false;
            } else {
                isOrderPut = true;
            }
        }

        public bool GetOrderStatus() => isOrderPut;

        public async void WaitToExpire(IOrder order, IAlpacaTradingClient client, int time) {
            Guid id = order.OrderId;
            await Task.Delay(TimeSpan.FromSeconds(time));
            Console.WriteLine("Time expired. Trying to cancel order");
            try {
                client.CancelOrderAsync(id);
                ChangeOrderStatus();
                Console.WriteLine(isOrderPut);
                Console.WriteLine("Order canceled");
            } catch {
                Console.WriteLine("Failed to cancel order!");
                throw new TaskCanceledException();
            }
            
        }
    }
}