using Microsoft.Extensions.Caching.Memory;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.Dex.Engine;
using System.Text.Json;

namespace LksBrothers.Wallet.Services;

public class WalletService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<WalletService> _logger;

    public WalletService(IMemoryCache cache, ILogger<WalletService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<WalletInfo> GetWalletInfoAsync(Address address)
    {
        try
        {
            // Get cached wallet info or create new
            var cacheKey = $"wallet_{address}";
            if (_cache.TryGetValue(cacheKey, out WalletInfo? cached))
            {
                return cached!;
            }

            // Create simple wallet info
            var walletInfo = new WalletInfo
            {
                Address = address,
                Balance = await GetBalanceAsync(address),
                LastUpdated = DateTimeOffset.UtcNow,
                RecentTransactions = await GetRecentTransactionsAsync(address)
            };

            // Cache for 30 seconds
            _cache.Set(cacheKey, walletInfo, TimeSpan.FromSeconds(30));
            return walletInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet info for {Address}", address);
            return new WalletInfo
            {
                Address = address,
                Balance = UInt256.Zero,
                LastUpdated = DateTimeOffset.UtcNow,
                RecentTransactions = new List<SimpleTransaction>()
            };
        }
    }

    public async Task<SendResult> SendLKSAsync(Address from, Address to, UInt256 amount)
    {
        try
        {
            // Create simple transaction
            var transaction = new Transaction
            {
                Hash = Hash.ComputeHash($"{from}{to}{amount}{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                From = from,
                To = to,
                Amount = amount,
                Timestamp = DateTimeOffset.UtcNow,
                Type = TransactionType.Transfer,
                Status = TransactionStatus.Pending
            };

            // Simulate transaction processing (in real implementation, this would go to the blockchain)
            await Task.Delay(100); // Simulate network delay
            
            transaction.Status = TransactionStatus.Confirmed;

            // Clear cache to force refresh
            _cache.Remove($"wallet_{from}");
            _cache.Remove($"wallet_{to}");

            _logger.LogInformation("Sent {Amount} LKS from {From} to {To}", amount, from, to);

            return new SendResult
            {
                Success = true,
                TransactionHash = transaction.Hash,
                Message = "Transaction sent successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending LKS from {From} to {To}", from, to);
            return new SendResult
            {
                Success = false,
                Message = $"Send failed: {ex.Message}"
            };
        }
    }

    public async Task<List<SimpleTransaction>> GetTransactionHistoryAsync(Address address, int limit = 10)
    {
        try
        {
            // Simulate getting transaction history
            return await GetRecentTransactionsAsync(address, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transaction history for {Address}", address);
            return new List<SimpleTransaction>();
        }
    }

    public string GenerateQRCode(Address address)
    {
        // Simple QR code data - just the address
        return address.ToString();
    }

    private async Task<UInt256> GetBalanceAsync(Address address)
    {
        // Simulate balance lookup
        await Task.Delay(10);
        
        // Return mock balance based on address hash
        var addressHash = address.ToString().GetHashCode();
        var balance = Math.Abs(addressHash) % 1000000; // 0 to 1M LKS
        return UInt256.Parse((balance * 1000000000000000000L).ToString()); // Convert to wei
    }

    private async Task<List<SimpleTransaction>> GetRecentTransactionsAsync(Address address, int limit = 5)
    {
        // Simulate recent transactions
        await Task.Delay(10);
        
        var transactions = new List<SimpleTransaction>();
        var random = new Random(address.ToString().GetHashCode());
        
        for (int i = 0; i < Math.Min(limit, 5); i++)
        {
            var isIncoming = random.Next(2) == 0;
            var amount = random.Next(1, 1000) * 1000000000000000000L; // 1-1000 LKS
            
            transactions.Add(new SimpleTransaction
            {
                Hash = Hash.ComputeHash($"tx_{address}_{i}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                From = isIncoming ? GenerateRandomAddress(random) : address,
                To = isIncoming ? address : GenerateRandomAddress(random),
                Amount = UInt256.Parse(amount.ToString()),
                Timestamp = DateTimeOffset.UtcNow.AddHours(-random.Next(1, 72)),
                Type = isIncoming ? "Received" : "Sent",
                Status = "Confirmed"
            });
        }
        
        return transactions.OrderByDescending(t => t.Timestamp).ToList();
    }

    private Address GenerateRandomAddress(Random random)
    {
        var bytes = new byte[20];
        random.NextBytes(bytes);
        return new Address(bytes);
    }
}

public class WalletInfo
{
    public required Address Address { get; set; }
    public required UInt256 Balance { get; set; }
    public required DateTimeOffset LastUpdated { get; set; }
    public required List<SimpleTransaction> RecentTransactions { get; set; }
    
    public string BalanceFormatted => FormatLKS(Balance);
    
    private string FormatLKS(UInt256 wei)
    {
        // Convert wei to LKS (divide by 10^18)
        var lks = (double)wei / 1000000000000000000.0;
        return lks.ToString("N2") + " LKS";
    }
}

public class SimpleTransaction
{
    public required Hash Hash { get; set; }
    public required Address From { get; set; }
    public required Address To { get; set; }
    public required UInt256 Amount { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    
    public string AmountFormatted => FormatLKS(Amount);
    public string TimeAgo => GetTimeAgo(Timestamp);
    
    private string FormatLKS(UInt256 wei)
    {
        var lks = (double)wei / 1000000000000000000.0;
        return lks.ToString("N2") + " LKS";
    }
    
    private string GetTimeAgo(DateTimeOffset timestamp)
    {
        var diff = DateTimeOffset.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
        return $"{(int)diff.TotalDays}d ago";
    }
}

public class SendResult
{
    public required bool Success { get; set; }
    public Hash? TransactionHash { get; set; }
    public required string Message { get; set; }
}
