using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.Contracts;
using Nethereum.RPC.Eth.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LksBrothers.Core.Blockchain
{
    public class LKSBlockchain
    {
        private readonly IWeb3 _web3;
        private readonly ILogger<LKSBlockchain> _logger;
        private readonly string _nodeUrl;
        private readonly int _chainId;
        private readonly string _lksCoinContractAddress;
        private readonly string _paymentSystemContractAddress;

        public LKSBlockchain(IConfiguration configuration, ILogger<LKSBlockchain> logger)
        {
            _logger = logger;
            _nodeUrl = configuration["LKSNetwork:NodeUrl"] ?? "http://localhost:8545";
            _chainId = int.Parse(configuration["LKSNetwork:ChainId"] ?? "1337");
            _lksCoinContractAddress = configuration["LKSNetwork:LKSCoinContract"] ?? "0x742d35Cc6634C0532925a3b8D4C9db96";
            _paymentSystemContractAddress = configuration["LKSNetwork:PaymentSystemContract"] ?? "0x1234567890123456789012345678901234567890";
            
            _web3 = new Web3(_nodeUrl);
        }

        public async Task<bool> IsConnectedAsync()
        {
            try
            {
                var blockNumber = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                return blockNumber != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to LKS Network node");
                return false;
            }
        }

        public async Task<BigInteger> GetLKSBalanceAsync(string address)
        {
            try
            {
                var contract = _web3.Eth.GetContract(LKSCoinABI, _lksCoinContractAddress);
                var balanceFunction = contract.GetFunction("balanceOf");
                var balance = await balanceFunction.CallAsync<BigInteger>(address);
                return balance;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get LKS balance for address {Address}", address);
                return 0;
            }
        }

        public async Task<string> SendLKSTransactionAsync(string fromAddress, string toAddress, BigInteger amount, string privateKey)
        {
            try
            {
                var account = new Nethereum.Web3.Accounts.Account(privateKey, _chainId);
                var web3 = new Web3(account, _nodeUrl);

                var contract = web3.Eth.GetContract(LKSCoinABI, _lksCoinContractAddress);
                var transferFunction = contract.GetFunction("transfer");

                var gas = await transferFunction.EstimateGasAsync(fromAddress, null, null, toAddress, amount);
                
                var receipt = await transferFunction.SendTransactionAndWaitForReceiptAsync(
                    fromAddress, 
                    gas, 
                    new Nethereum.Hex.HexTypes.HexBigInteger(0), // Zero gas price
                    null, 
                    toAddress, 
                    amount
                );

                _logger.LogInformation("LKS transaction sent: {TxHash}", receipt.TransactionHash);
                return receipt.TransactionHash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send LKS transaction");
                throw;
            }
        }

        public async Task<string> ProcessServicePaymentAsync(string userAddress, int serviceId, BigInteger amount, string privateKey)
        {
            try
            {
                var account = new Nethereum.Web3.Accounts.Account(privateKey, _chainId);
                var web3 = new Web3(account, _nodeUrl);

                var contract = web3.Eth.GetContract(PaymentSystemABI, _paymentSystemContractAddress);
                var paymentFunction = contract.GetFunction("processPayment");

                var serviceAddress = GetServiceAddress(serviceId);
                var gas = await paymentFunction.EstimateGasAsync(userAddress, null, null, serviceId, amount, serviceAddress);
                
                var receipt = await paymentFunction.SendTransactionAndWaitForReceiptAsync(
                    userAddress,
                    gas,
                    new Nethereum.Hex.HexTypes.HexBigInteger(0), // Zero gas price
                    null,
                    serviceId,
                    amount,
                    serviceAddress
                );

                _logger.LogInformation("Service payment processed: {TxHash} for service {ServiceId}", receipt.TransactionHash, serviceId);
                return receipt.TransactionHash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process service payment for service {ServiceId}", serviceId);
                throw;
            }
        }

        public async Task<TransactionReceipt> GetTransactionReceiptAsync(string txHash)
        {
            try
            {
                return await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txHash);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction receipt for {TxHash}", txHash);
                return null;
            }
        }

        public async Task<BlockWithTransactions> GetLatestBlockAsync()
        {
            try
            {
                return await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get latest block");
                return null;
            }
        }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            try
            {
                return await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get block number");
                return 0;
            }
        }

        public async Task<List<Transaction>> GetTransactionHistoryAsync(string address, int limit = 100)
        {
            try
            {
                var transactions = new List<Transaction>();
                var latestBlock = await GetBlockNumberAsync();
                
                // Scan recent blocks for transactions involving this address
                for (var i = 0; i < Math.Min(1000, (int)latestBlock) && transactions.Count < limit; i++)
                {
                    var blockNumber = latestBlock - i;
                    var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                        new BlockParameter((ulong)blockNumber));
                    
                    if (block?.Transactions != null)
                    {
                        foreach (var tx in block.Transactions)
                        {
                            if (tx.From?.Equals(address, StringComparison.OrdinalIgnoreCase) == true ||
                                tx.To?.Equals(address, StringComparison.OrdinalIgnoreCase) == true)
                            {
                                transactions.Add(tx);
                                if (transactions.Count >= limit) break;
                            }
                        }
                    }
                }

                return transactions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get transaction history for {Address}", address);
                return new List<Transaction>();
            }
        }

        private string GetServiceAddress(int serviceId)
        {
            return serviceId switch
            {
                0 => "0x1111111111111111111111111111111111111111", // IP Patent
                1 => "0x2222222222222222222222222222222222222222", // LKS Summit
                2 => "0x3333333333333333333333333333333333333333", // Software Factory
                3 => "0x4444444444444444444444444444444444444444", // Vara Security
                4 => "0x5555555555555555555555555555555555555555", // Stadium Tackle
                5 => "0x6666666666666666666666666666666666666666", // LKS Capital
                _ => throw new ArgumentException($"Invalid service ID: {serviceId}")
            };
        }

        private static readonly string LKSCoinABI = @"[
            {
                'constant': true,
                'inputs': [{'name': '_owner', 'type': 'address'}],
                'name': 'balanceOf',
                'outputs': [{'name': 'balance', 'type': 'uint256'}],
                'type': 'function'
            },
            {
                'constant': false,
                'inputs': [
                    {'name': '_to', 'type': 'address'},
                    {'name': '_value', 'type': 'uint256'}
                ],
                'name': 'transfer',
                'outputs': [{'name': '', 'type': 'bool'}],
                'type': 'function'
            },
            {
                'constant': true,
                'inputs': [],
                'name': 'totalSupply',
                'outputs': [{'name': '', 'type': 'uint256'}],
                'type': 'function'
            }
        ]";

        private static readonly string PaymentSystemABI = @"[
            {
                'constant': false,
                'inputs': [
                    {'name': '_service', 'type': 'uint8'},
                    {'name': '_amount', 'type': 'uint256'},
                    {'name': '_recipient', 'type': 'address'}
                ],
                'name': 'processPayment',
                'outputs': [{'name': '', 'type': 'bool'}],
                'type': 'function'
            },
            {
                'constant': true,
                'inputs': [{'name': '_service', 'type': 'uint8'}],
                'name': 'getServiceFee',
                'outputs': [{'name': '', 'type': 'uint256'}],
                'type': 'function'
            }
        ]";
    }
}
