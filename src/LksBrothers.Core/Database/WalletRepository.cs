using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Dapper;

namespace LksBrothers.Core.Database
{
    public class WalletRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<WalletRepository> _logger;

        public WalletRepository(IConfiguration configuration, ILogger<WalletRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("LKSDatabase");
            _logger = logger;
        }

        public async Task<WalletData> GetWalletAsync(string address)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                const string sql = @"
                    SELECT Address, LKSBalance, ETHBalance, LastUpdated, IsActive
                    FROM Wallets 
                    WHERE Address = @Address";
                
                return await connection.QueryFirstOrDefaultAsync<WalletData>(sql, new { Address = address });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get wallet data for {Address}", address);
                return null;
            }
        }

        public async Task<bool> CreateWalletAsync(WalletData wallet)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                const string sql = @"
                    INSERT INTO Wallets (Address, LKSBalance, ETHBalance, LastUpdated, IsActive, CreatedAt)
                    VALUES (@Address, @LKSBalance, @ETHBalance, @LastUpdated, @IsActive, @CreatedAt)";
                
                var result = await connection.ExecuteAsync(sql, wallet);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create wallet for {Address}", wallet.Address);
                return false;
            }
        }

        public async Task<bool> UpdateWalletBalanceAsync(string address, decimal lksBalance, decimal ethBalance)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                const string sql = @"
                    UPDATE Wallets 
                    SET LKSBalance = @LKSBalance, ETHBalance = @ETHBalance, LastUpdated = @LastUpdated
                    WHERE Address = @Address";
                
                var result = await connection.ExecuteAsync(sql, new 
                { 
                    Address = address, 
                    LKSBalance = lksBalance, 
                    ETHBalance = ethBalance, 
                    LastUpdated = DateTime.UtcNow 
                });
                
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update wallet balance for {Address}", address);
                return false;
            }
        }

        public async Task<List<TransactionRecord>> GetTransactionHistoryAsync(string address, int limit = 100)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                const string sql = @"
                    SELECT TOP (@Limit) TxHash, FromAddress, ToAddress, Amount, Timestamp, 
                           TransactionType, ServiceId, Status, GasUsed, GasFee, Note
                    FROM Transactions 
                    WHERE FromAddress = @Address OR ToAddress = @Address
                    ORDER BY Timestamp DESC";
                
                var transactions = await connection.QueryAsync<TransactionRecord>(sql, new { Address = address, Limit = limit });
                return transactions.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction history for {Address}", address);
                return new List<TransactionRecord>();
            }
        }

        public async Task<bool> RecordTransactionAsync(TransactionRecord transaction)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                const string sql = @"
                    INSERT INTO Transactions (TxHash, FromAddress, ToAddress, Amount, Timestamp, 
                                            TransactionType, ServiceId, Status, GasUsed, GasFee, Note)
                    VALUES (@TxHash, @FromAddress, @ToAddress, @Amount, @Timestamp, 
                            @TransactionType, @ServiceId, @Status, @GasUsed, @GasFee, @Note)";
                
                var result = await connection.ExecuteAsync(sql, transaction);
                return result > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record transaction {TxHash}", transaction.TxHash);
                return false;
            }
        }

        public async Task<TransactionRecord> GetTransactionAsync(string txHash)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                const string sql = @"
                    SELECT TxHash, FromAddress, ToAddress, Amount, Timestamp, 
                           TransactionType, ServiceId, Status, GasUsed, GasFee, Note
                    FROM Transactions 
                    WHERE TxHash = @TxHash";
                
                return await connection.QueryFirstOrDefaultAsync<TransactionRecord>(sql, new { TxHash = txHash });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction {TxHash}", txHash);
                return null;
            }
        }

        public async Task<NetworkStats> GetNetworkStatsAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                const string sql = @"
                    SELECT 
                        COUNT(DISTINCT FromAddress) + COUNT(DISTINCT ToAddress) as ActiveWallets,
                        COUNT(*) as TotalTransactions,
                        SUM(Amount) as TotalVolume,
                        AVG(CAST(GasUsed as FLOAT)) as AvgGasUsed
                    FROM Transactions 
                    WHERE Timestamp >= DATEADD(day, -1, GETUTCDATE())";
                
                return await connection.QueryFirstOrDefaultAsync<NetworkStats>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get network stats");
                return new NetworkStats();
            }
        }
    }

    public class WalletData
    {
        public string Address { get; set; }
        public decimal LKSBalance { get; set; }
        public decimal ETHBalance { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TransactionRecord
    {
        public string TxHash { get; set; }
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public decimal Amount { get; set; }
        public DateTime Timestamp { get; set; }
        public string TransactionType { get; set; }
        public int? ServiceId { get; set; }
        public string Status { get; set; }
        public long GasUsed { get; set; }
        public decimal GasFee { get; set; }
        public string Note { get; set; }
    }

    public class NetworkStats
    {
        public int ActiveWallets { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalVolume { get; set; }
        public double AvgGasUsed { get; set; }
    }
}
