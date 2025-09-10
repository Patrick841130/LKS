using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.Hooks.Engine;
using MessagePack;
using System.Collections.Concurrent;

namespace LksBrothers.Dex.Engine;

public class DexEngine : IDisposable
{
    private readonly ILogger<DexEngine> _logger;
    private readonly DexEngineOptions _options;
    private readonly IMemoryCache _cache;
    private readonly HookExecutor _hookExecutor;
    private readonly ConcurrentDictionary<Hash, Order> _activeOrders;
    private readonly ConcurrentDictionary<string, TradingPair> _tradingPairs;
    private readonly ConcurrentQueue<Trade> _recentTrades;
    private readonly Timer _marketDataTimer;

    public DexEngine(
        ILogger<DexEngine> logger,
        IOptions<DexEngineOptions> options,
        IMemoryCache cache,
        HookExecutor hookExecutor)
    {
        _logger = logger;
        _options = options.Value;
        _cache = cache;
        _hookExecutor = hookExecutor;
        _activeOrders = new ConcurrentDictionary<Hash, Order>();
        _tradingPairs = new ConcurrentDictionary<string, TradingPair>();
        _recentTrades = new ConcurrentQueue<Trade>();
        
        // Timer for market data updates
        _marketDataTimer = new Timer(UpdateMarketData, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        
        InitializeTradingPairs();
        _logger.LogInformation("DEX engine initialized with {PairCount} trading pairs", _tradingPairs.Count);
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
    {
        try
        {
            // Validate trading pair
            if (!_tradingPairs.TryGetValue(request.TradingPair, out var pair))
            {
                return OrderResult.Failed("Invalid trading pair");
            }

            // Create order
            var order = new Order
            {
                Id = Hash.ComputeHash($"{request.Account}{request.TradingPair}{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                Account = request.Account,
                TradingPair = request.TradingPair,
                Type = request.Type,
                Side = request.Side,
                Amount = request.Amount,
                Price = request.Price,
                Status = OrderStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = request.ExpiresAt ?? DateTimeOffset.UtcNow.AddDays(30)
            };

            // Validate order through hooks (zero-fee processing)
            var hookResult = await _hookExecutor.ExecuteZeroFeeHookAsync(new LksCoinTransaction
            {
                Hash = order.Id,
                From = request.Account,
                To = pair.BaseAsset, // Simplified
                Amount = request.Amount,
                Type = TransactionType.Transfer,
                NetworkFee = UInt256.Zero,
                UserFee = UInt256.Zero,
                Timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ZeroFeeSponsored = true
            });

            if (!hookResult.Success)
            {
                return OrderResult.Failed($"Hook validation failed: {hookResult.Message}");
            }

            // Add to active orders
            _activeOrders[order.Id] = order;
            order.Status = OrderStatus.Active;

            // Try to match immediately
            await TryMatchOrderAsync(order);

            _logger.LogInformation("Created order {OrderId} for {Amount} {Pair} at {Price}", 
                order.Id, request.Amount, request.TradingPair, request.Price);

            return OrderResult.Success(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for {Account}", request.Account);
            return OrderResult.Failed($"Order creation error: {ex.Message}");
        }
    }

    public async Task<OrderResult> CancelOrderAsync(Hash orderId, Address account)
    {
        try
        {
            if (!_activeOrders.TryGetValue(orderId, out var order))
            {
                return OrderResult.Failed("Order not found");
            }

            if (!order.Account.Equals(account))
            {
                return OrderResult.Failed("Unauthorized to cancel this order");
            }

            if (order.Status != OrderStatus.Active)
            {
                return OrderResult.Failed("Order cannot be cancelled");
            }

            // Remove from active orders
            _activeOrders.TryRemove(orderId, out _);
            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTimeOffset.UtcNow;

            _logger.LogInformation("Cancelled order {OrderId} for account {Account}", orderId, account);
            
            return OrderResult.Success(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            return OrderResult.Failed($"Order cancellation error: {ex.Message}");
        }
    }

    public async Task<List<Order>> GetActiveOrdersAsync(Address? account = null, string? tradingPair = null)
    {
        var orders = _activeOrders.Values.Where(o => o.Status == OrderStatus.Active);

        if (account != null)
            orders = orders.Where(o => o.Account.Equals(account));

        if (!string.IsNullOrEmpty(tradingPair))
            orders = orders.Where(o => o.TradingPair == tradingPair);

        return orders.OrderByDescending(o => o.CreatedAt).ToList();
    }

    public async Task<OrderBook> GetOrderBookAsync(string tradingPair)
    {
        var cacheKey = $"orderbook:{tradingPair}";
        if (_cache.TryGetValue(cacheKey, out OrderBook? cachedOrderBook))
        {
            return cachedOrderBook!;
        }

        var orders = _activeOrders.Values
            .Where(o => o.TradingPair == tradingPair && o.Status == OrderStatus.Active)
            .ToList();

        var buyOrders = orders
            .Where(o => o.Side == OrderSide.Buy)
            .OrderByDescending(o => o.Price)
            .ThenBy(o => o.CreatedAt)
            .Take(50)
            .ToList();

        var sellOrders = orders
            .Where(o => o.Side == OrderSide.Sell)
            .OrderBy(o => o.Price)
            .ThenBy(o => o.CreatedAt)
            .Take(50)
            .ToList();

        var orderBook = new OrderBook
        {
            TradingPair = tradingPair,
            BuyOrders = buyOrders,
            SellOrders = sellOrders,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _cache.Set(cacheKey, orderBook, TimeSpan.FromSeconds(1));
        return orderBook;
    }

    public async Task<MarketData> GetMarketDataAsync(string tradingPair)
    {
        var cacheKey = $"market:{tradingPair}";
        if (_cache.TryGetValue(cacheKey, out MarketData? cachedData))
        {
            return cachedData!;
        }

        var recentTrades = _recentTrades
            .Where(t => t.TradingPair == tradingPair && t.Timestamp > DateTimeOffset.UtcNow.AddHours(-24))
            .OrderByDescending(t => t.Timestamp)
            .ToList();

        var marketData = new MarketData
        {
            TradingPair = tradingPair,
            LastPrice = recentTrades.FirstOrDefault()?.Price ?? UInt256.Zero,
            Volume24h = recentTrades.Sum(t => (decimal)t.Amount),
            High24h = recentTrades.Any() ? recentTrades.Max(t => t.Price) : UInt256.Zero,
            Low24h = recentTrades.Any() ? recentTrades.Min(t => t.Price) : UInt256.Zero,
            PriceChange24h = CalculatePriceChange(recentTrades),
            TradeCount24h = recentTrades.Count,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _cache.Set(cacheKey, marketData, TimeSpan.FromSeconds(5));
        return marketData;
    }

    public async Task<List<Trade>> GetRecentTradesAsync(string tradingPair, int limit = 50)
    {
        return _recentTrades
            .Where(t => t.TradingPair == tradingPair)
            .OrderByDescending(t => t.Timestamp)
            .Take(limit)
            .ToList();
    }

    private async Task TryMatchOrderAsync(Order order)
    {
        try
        {
            var oppositeOrders = _activeOrders.Values
                .Where(o => o.TradingPair == order.TradingPair && 
                           o.Side != order.Side && 
                           o.Status == OrderStatus.Active)
                .OrderBy(o => order.Side == OrderSide.Buy ? o.Price : -o.Price)
                .ThenBy(o => o.CreatedAt)
                .ToList();

            foreach (var matchOrder in oppositeOrders)
            {
                if (order.RemainingAmount <= UInt256.Zero)
                    break;

                if (!CanMatch(order, matchOrder))
                    continue;

                var tradeAmount = UInt256.Min(order.RemainingAmount, matchOrder.RemainingAmount);
                var tradePrice = matchOrder.Price; // Price discovery: taker gets maker price

                // Execute trade
                var trade = new Trade
                {
                    Id = Hash.ComputeHash($"{order.Id}{matchOrder.Id}{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                    TradingPair = order.TradingPair,
                    BuyOrderId = order.Side == OrderSide.Buy ? order.Id : matchOrder.Id,
                    SellOrderId = order.Side == OrderSide.Sell ? order.Id : matchOrder.Id,
                    Buyer = order.Side == OrderSide.Buy ? order.Account : matchOrder.Account,
                    Seller = order.Side == OrderSide.Sell ? order.Account : matchOrder.Account,
                    Amount = tradeAmount,
                    Price = tradePrice,
                    Timestamp = DateTimeOffset.UtcNow
                };

                // Update order amounts
                order.FilledAmount += tradeAmount;
                matchOrder.FilledAmount += tradeAmount;

                // Update order status
                if (order.RemainingAmount <= UInt256.Zero)
                {
                    order.Status = OrderStatus.Filled;
                    _activeOrders.TryRemove(order.Id, out _);
                }

                if (matchOrder.RemainingAmount <= UInt256.Zero)
                {
                    matchOrder.Status = OrderStatus.Filled;
                    _activeOrders.TryRemove(matchOrder.Id, out _);
                }

                // Record trade
                _recentTrades.Enqueue(trade);
                
                // Clean up old trades
                while (_recentTrades.Count > 10000)
                {
                    _recentTrades.TryDequeue(out _);
                }

                _logger.LogInformation("Executed trade {TradeId}: {Amount} {Pair} at {Price}", 
                    trade.Id, tradeAmount, order.TradingPair, tradePrice);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching order {OrderId}", order.Id);
        }
    }

    private bool CanMatch(Order order1, Order order2)
    {
        if (order1.Side == order2.Side)
            return false;

        // For buy orders, can match if buy price >= sell price
        // For sell orders, can match if sell price <= buy price
        if (order1.Side == OrderSide.Buy)
            return order1.Price >= order2.Price;
        else
            return order1.Price <= order2.Price;
    }

    private decimal CalculatePriceChange(List<Trade> trades)
    {
        if (trades.Count < 2)
            return 0;

        var latestPrice = (decimal)trades.First().Price;
        var oldestPrice = (decimal)trades.Last().Price;

        if (oldestPrice == 0)
            return 0;

        return ((latestPrice - oldestPrice) / oldestPrice) * 100;
    }

    private void InitializeTradingPairs()
    {
        // Initialize LKS COIN trading pairs
        var pairs = new[]
        {
            new TradingPair { Symbol = "LKS/XRP", BaseAsset = Address.Parse("rLKSFoundationAddress"), QuoteAsset = Address.Parse("rXRPAddress"), MinOrderSize = new UInt256(1000000000000000000UL) }, // 1 LKS
            new TradingPair { Symbol = "LKS/USD", BaseAsset = Address.Parse("rLKSFoundationAddress"), QuoteAsset = Address.Parse("rUSDAddress"), MinOrderSize = new UInt256(1000000000000000000UL) }, // 1 LKS
            new TradingPair { Symbol = "LKS/BTC", BaseAsset = Address.Parse("rLKSFoundationAddress"), QuoteAsset = Address.Parse("rBTCAddress"), MinOrderSize = new UInt256(1000000000000000000UL) }, // 1 LKS
            new TradingPair { Symbol = "LKS/ETH", BaseAsset = Address.Parse("rLKSFoundationAddress"), QuoteAsset = Address.Parse("rETHAddress"), MinOrderSize = new UInt256(1000000000000000000UL) }  // 1 LKS
        };

        foreach (var pair in pairs)
        {
            _tradingPairs[pair.Symbol] = pair;
        }
    }

    private void UpdateMarketData(object? state)
    {
        try
        {
            // Clear market data cache to force refresh
            foreach (var pair in _tradingPairs.Keys)
            {
                _cache.Remove($"market:{pair}");
                _cache.Remove($"orderbook:{pair}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating market data");
        }
    }

    public void Dispose()
    {
        _marketDataTimer?.Dispose();
        _logger.LogInformation("DEX engine disposed");
    }
}

[MessagePackObject]
public class Order
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required Address Account { get; set; }

    [Key(2)]
    public required string TradingPair { get; set; }

    [Key(3)]
    public required OrderType Type { get; set; }

    [Key(4)]
    public required OrderSide Side { get; set; }

    [Key(5)]
    public required UInt256 Amount { get; set; }

    [Key(6)]
    public required UInt256 Price { get; set; }

    [Key(7)]
    public required OrderStatus Status { get; set; }

    [Key(8)]
    public UInt256 FilledAmount { get; set; } = UInt256.Zero;

    [Key(9)]
    public required DateTimeOffset CreatedAt { get; set; }

    [Key(10)]
    public DateTimeOffset? UpdatedAt { get; set; }

    [Key(11)]
    public required DateTimeOffset ExpiresAt { get; set; }

    public UInt256 RemainingAmount => Amount - FilledAmount;
}

[MessagePackObject]
public class Trade
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required string TradingPair { get; set; }

    [Key(2)]
    public required Hash BuyOrderId { get; set; }

    [Key(3)]
    public required Hash SellOrderId { get; set; }

    [Key(4)]
    public required Address Buyer { get; set; }

    [Key(5)]
    public required Address Seller { get; set; }

    [Key(6)]
    public required UInt256 Amount { get; set; }

    [Key(7)]
    public required UInt256 Price { get; set; }

    [Key(8)]
    public required DateTimeOffset Timestamp { get; set; }
}

[MessagePackObject]
public class TradingPair
{
    [Key(0)]
    public required string Symbol { get; set; }

    [Key(1)]
    public required Address BaseAsset { get; set; }

    [Key(2)]
    public required Address QuoteAsset { get; set; }

    [Key(3)]
    public required UInt256 MinOrderSize { get; set; }

    [Key(4)]
    public bool IsActive { get; set; } = true;
}

public class OrderBook
{
    public required string TradingPair { get; set; }
    public required List<Order> BuyOrders { get; set; }
    public required List<Order> SellOrders { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}

public class MarketData
{
    public required string TradingPair { get; set; }
    public required UInt256 LastPrice { get; set; }
    public required decimal Volume24h { get; set; }
    public required UInt256 High24h { get; set; }
    public required UInt256 Low24h { get; set; }
    public required decimal PriceChange24h { get; set; }
    public required int TradeCount24h { get; set; }
    public required DateTimeOffset UpdatedAt { get; set; }
}

public class CreateOrderRequest
{
    public required Address Account { get; set; }
    public required string TradingPair { get; set; }
    public required OrderType Type { get; set; }
    public required OrderSide Side { get; set; }
    public required UInt256 Amount { get; set; }
    public required UInt256 Price { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public class OrderResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Order? Order { get; set; }

    public static OrderResult Success(Order order)
    {
        return new OrderResult { Success = true, Order = order };
    }

    public static OrderResult Failed(string error)
    {
        return new OrderResult { Success = false, ErrorMessage = error };
    }
}

public enum OrderType
{
    Market,
    Limit,
    StopLoss,
    StopLimit
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderStatus
{
    Pending,
    Active,
    PartiallyFilled,
    Filled,
    Cancelled,
    Expired,
    Rejected
}

public class DexEngineOptions
{
    public int MaxOrdersPerAccount { get; set; } = 100;
    public int MaxTradeHistory { get; set; } = 10000;
    public TimeSpan OrderExpiry { get; set; } = TimeSpan.FromDays(30);
    public bool EnableZeroFees { get; set; } = true;
}
