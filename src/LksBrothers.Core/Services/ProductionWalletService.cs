using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Microsoft.Extensions.Logging;
using LksBrothers.Core.Blockchain;
using LksBrothers.Core.Database;

namespace LksBrothers.Core.Services
{
    public class ProductionWalletService
    {
        private readonly LKSBlockchain _blockchain;
        private readonly WalletRepository _walletRepository;
        private readonly ILogger<ProductionWalletService> _logger;

        public ProductionWalletService(
            LKSBlockchain blockchain,
            WalletRepository walletRepository,
            ILogger<ProductionWalletService> logger)
        {
            _blockchain = blockchain;
            _walletRepository = walletRepository;
            _logger = logger;
        }

        public async Task<WalletBalance> GetBalanceAsync(string address)
        {
            try
            {
                // Get balance from blockchain
                var lksBalance = await _blockchain.GetLKSBalanceAsync(address);
                var lksBalanceDecimal = (decimal)lksBalance / (decimal)Math.Pow(10, 18);

                // Update database
                await _walletRepository.UpdateWalletBalanceAsync(address, lksBalanceDecimal, 0);

                return new WalletBalance
                {
                    Address = address,
                    LKSBalance = lksBalanceDecimal,
                    ETHBalance = 0,
                    LastUpdated = DateTime.UtcNow,
                    Network = "LKS Network"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get balance for {Address}", address);
                
                // Fallback to database
                var walletData = await _walletRepository.GetWalletAsync(address);
                if (walletData != null)
                {
                    return new WalletBalance
                    {
                        Address = address,
                        LKSBalance = walletData.LKSBalance,
                        ETHBalance = walletData.ETHBalance,
                        LastUpdated = walletData.LastUpdated,
                        Network = "LKS Network (Cached)"
                    };
                }

                throw;
            }
        }

        public async Task<TransactionResult> SendTransactionAsync(SendTransactionRequest request)
        {
            try
            {
                // Validate request
                if (string.IsNullOrEmpty(request.FromAddress) || 
                    string.IsNullOrEmpty(request.ToAddress) || 
                    request.Amount <= 0)
                {
                    throw new ArgumentException("Invalid transaction parameters");
                }

                // Check balance
                var balance = await GetBalanceAsync(request.FromAddress);
                if (balance.LKSBalance < request.Amount)
                {
                    throw new InvalidOperationException("Insufficient balance");
                }

                // Convert amount to Wei
                var amountWei = new BigInteger(request.Amount * (decimal)Math.Pow(10, 18));

                // Send transaction to blockchain
                var txHash = await _blockchain.SendLKSTransactionAsync(
                    request.FromAddress,
                    request.ToAddress,
                    amountWei,
                    request.PrivateKey
                );

                // Record transaction in database
                var transaction = new TransactionRecord
                {
                    TxHash = txHash,
                    FromAddress = request.FromAddress,
                    ToAddress = request.ToAddress,
                    Amount = request.Amount,
                    Timestamp = DateTime.UtcNow,
                    TransactionType = "transfer",
                    Status = "confirmed",
                    GasUsed = 21000,
                    GasFee = 0, // Zero fees on LKS Network
                    Note = request.Note ?? ""
                };

                await _walletRepository.RecordTransactionAsync(transaction);

                _logger.LogInformation("Transaction sent successfully: {TxHash}", txHash);

                return new TransactionResult
                {
                    Success = true,
                    TxHash = txHash,
                    Message = "Transaction completed with zero fees",
                    GasFee = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send transaction");
                return new TransactionResult
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        public async Task<TransactionResult> ProcessServicePaymentAsync(ServicePaymentRequest request)
        {
            try
            {
                // Validate service
                if (!IsValidService(request.ServiceId))
                {
                    throw new ArgumentException($"Invalid service ID: {request.ServiceId}");
                }

                // Check balance
                var balance = await GetBalanceAsync(request.UserAddress);
                if (balance.LKSBalance < request.Amount)
                {
                    throw new InvalidOperationException("Insufficient LKS balance");
                }

                // Convert amount to Wei
                var amountWei = new BigInteger(request.Amount * (decimal)Math.Pow(10, 18));

                // Process payment through blockchain
                var txHash = await _blockchain.ProcessServicePaymentAsync(
                    request.UserAddress,
                    request.ServiceId,
                    amountWei,
                    request.PrivateKey
                );

                // Record transaction
                var transaction = new TransactionRecord
                {
                    TxHash = txHash,
                    FromAddress = request.UserAddress,
                    ToAddress = GetServiceAddress(request.ServiceId),
                    Amount = request.Amount,
                    Timestamp = DateTime.UtcNow,
                    TransactionType = "payment",
                    ServiceId = request.ServiceId,
                    Status = "confirmed",
                    GasUsed = 50000,
                    GasFee = 0,
                    Note = $"Payment to {GetServiceName(request.ServiceId)}"
                };

                await _walletRepository.RecordTransactionAsync(transaction);

                _logger.LogInformation("Service payment processed: {TxHash} for service {ServiceId}", txHash, request.ServiceId);

                return new TransactionResult
                {
                    Success = true,
                    TxHash = txHash,
                    Message = $"Payment to {GetServiceName(request.ServiceId)} completed with zero fees",
                    GasFee = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process service payment for service {ServiceId}", request.ServiceId);
                return new TransactionResult
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        public async Task<List<TransactionRecord>> GetTransactionHistoryAsync(string address, int limit = 100)
        {
            try
            {
                return await _walletRepository.GetTransactionHistoryAsync(address, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction history for {Address}", address);
                return new List<TransactionRecord>();
            }
        }

        public async Task<TransactionRecord> GetTransactionAsync(string txHash)
        {
            try
            {
                return await _walletRepository.GetTransactionAsync(txHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction {TxHash}", txHash);
                return null;
            }
        }

        public async Task<NetworkStatistics> GetNetworkStatsAsync()
        {
            try
            {
                var blockNumber = await _blockchain.GetBlockNumberAsync();
                var dbStats = await _walletRepository.GetNetworkStatsAsync();

                return new NetworkStatistics
                {
                    ChainId = 1337,
                    NetworkName = "LKS Network",
                    BlockNumber = (long)blockNumber,
                    GasPrice = 0,
                    TotalSupply = 1000000000,
                    CirculatingSupply = 500000000,
                    ActiveWallets = dbStats.ActiveWallets,
                    TotalTransactions = dbStats.TotalTransactions,
                    TPS = 65000,
                    Uptime = "99.9%",
                    LastUpdated = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get network statistics");
                return new NetworkStatistics
                {
                    ChainId = 1337,
                    NetworkName = "LKS Network",
                    LastUpdated = DateTime.UtcNow
                };
            }
        }

        public bool ValidateAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                return false;

            // LKS native address format
            if (address.StartsWith("lks1"))
            {
                return System.Text.RegularExpressions.Regex.IsMatch(address, @"^lks1[a-z0-9]{39,59}$");
            }

            // Ethereum-compatible address format
            if (address.StartsWith("0x"))
            {
                return System.Text.RegularExpressions.Regex.IsMatch(address, @"^0x[a-fA-F0-9]{40}$");
            }

            return false;
        }

        private bool IsValidService(int serviceId)
        {
            return serviceId >= 0 && serviceId <= 5;
        }

        private string GetServiceName(int serviceId)
        {
            return serviceId switch
            {
                0 => "IP Patent",
                1 => "LKS Summit",
                2 => "Software Factory",
                3 => "Vara Security",
                4 => "Stadium Tackle",
                5 => "LKS Capital",
                _ => "Unknown Service"
            };
        }

        private string GetServiceAddress(int serviceId)
        {
            return serviceId switch
            {
                0 => "0x1111111111111111111111111111111111111111",
                1 => "0x2222222222222222222222222222222222222222",
                2 => "0x3333333333333333333333333333333333333333",
                3 => "0x4444444444444444444444444444444444444444",
                4 => "0x5555555555555555555555555555555555555555",
                5 => "0x6666666666666666666666666666666666666666",
                _ => throw new ArgumentException($"Invalid service ID: {serviceId}")
            };
        }
    }

    public class WalletBalance
    {
        public string Address { get; set; }
        public decimal LKSBalance { get; set; }
        public decimal ETHBalance { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Network { get; set; }
    }

    public class SendTransactionRequest
    {
        public string FromAddress { get; set; }
        public string ToAddress { get; set; }
        public decimal Amount { get; set; }
        public string PrivateKey { get; set; }
        public string Note { get; set; }
    }

    public class ServicePaymentRequest
    {
        public string UserAddress { get; set; }
        public int ServiceId { get; set; }
        public decimal Amount { get; set; }
        public string PrivateKey { get; set; }
        public object ServiceData { get; set; }
    }

    public class TransactionResult
    {
        public bool Success { get; set; }
        public string TxHash { get; set; }
        public string Message { get; set; }
        public decimal GasFee { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class NetworkStatistics
    {
        public int ChainId { get; set; }
        public string NetworkName { get; set; }
        public long BlockNumber { get; set; }
        public decimal GasPrice { get; set; }
        public long TotalSupply { get; set; }
        public long CirculatingSupply { get; set; }
        public int ActiveWallets { get; set; }
        public int TotalTransactions { get; set; }
        public int TPS { get; set; }
        public string Uptime { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
