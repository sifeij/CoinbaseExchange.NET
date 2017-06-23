using System;

namespace CoinbaseExchange.NET.Endpoints.OrderBook
{
    public class BidAskOrder
    {
        public decimal Price { get; set; }
        public decimal Size { get; set; }
        public Guid Id { get; set; }
    }
}
