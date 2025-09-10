using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using LksBrothers.Core.Primitives;
using LksBrothers.Core.Models;

namespace LksBrothers.Rpc.Services;

public class JsonRpcService
{
    private readonly ILogger<JsonRpcService> _logger;
    private readonly IBlockchainService _blockchainService;
    private readonly Dictionary<string, Func<JsonElement[], Task<object>>> _methods;

    public JsonRpcService(
        ILogger<JsonRpcService> logger,
        IBlockchainService blockchainService)
    {
        _logger = logger;
        _blockchainService = blockchainService;
        _methods = InitializeMethods();
    }

    public async Task<JsonRpcResponse> ProcessRequestAsync(JsonRpcRequest request)
    {
        try
        {
            if (!_methods.TryGetValue(request.Method, out var handler))
            {
                return new JsonRpcResponse
                {
                    Id = request.Id,
                    Error = new JsonRpcError
                    {
                        Code = -32601,
                        Message = "Method not found",
                        Data = $"Method '{request.Method}' is not supported"
                    }
                };
            }

            var result = await handler(request.Params ?? Array.Empty<JsonElement>());
            
            return new JsonRpcResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (ArgumentException ex)
        {
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32602,
                    Message = "Invalid params",
                    Data = ex.Message
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing JSON-RPC request {Method}", request.Method);
            return new JsonRpcResponse
            {
                Id = request.Id,
                Error = new JsonRpcError
                {
                    Code = -32603,
                    Message = "Internal error",
                    Data = "An internal error occurred while processing the request"
                }
            };
        }
    }

    private Dictionary<string, Func<JsonElement[], Task<object>>> InitializeMethods()
    {
        return new Dictionary<string, Func<JsonElement[], Task<object>>>
        {
            // Blockchain methods
            ["lks_chainId"] = async _ =>
            {
                var status = await _blockchainService.GetStatusAsync();
                return status.ChainId;
            },

            ["lks_blockNumber"] = async _ =>
            {
                var status = await _blockchainService.GetStatusAsync();
                return $"0x{status.BlockHeight:x}";
            },

            ["lks_getBalance"] = async (parameters) =>
            {
                if (parameters.Length < 1)
                    throw new ArgumentException("Missing address parameter");

                var address = Address.Parse(parameters[0].GetString()!);
                var balance = await _blockchainService.GetBalanceAsync(address);
                return $"0x{balance:x}";
            },

            ["lks_getTransactionCount"] = async (parameters) =>
            {
                if (parameters.Length < 1)
                    throw new ArgumentException("Missing address parameter");

                var address = Address.Parse(parameters[0].GetString()!);
                var nonce = await _blockchainService.GetNonceAsync(address);
                return $"0x{nonce:x}";
            },

            ["lks_getBlockByNumber"] = async (parameters) =>
            {
                if (parameters.Length < 1)
                    throw new ArgumentException("Missing block number parameter");

                var blockNumber = parameters[0].GetString() == "latest" 
                    ? (await _blockchainService.GetStatusAsync()).BlockHeight
                    : Convert.ToUInt64(parameters[0].GetString()!.Replace("0x", ""), 16);

                var includeTransactions = parameters.Length > 1 && parameters[1].GetBoolean();
                var block = await _blockchainService.GetBlockAsync(blockNumber);

                if (block == null)
                    return null!;

                return new
                {
                    number = $"0x{block.Header.Number:x}",
                    hash = block.Hash.ToString(),
                    parentHash = block.Header.ParentHash.ToString(),
                    timestamp = $"0x{block.Header.Timestamp:x}",
                    gasUsed = $"0x{block.Header.GasUsed:x}",
                    gasLimit = $"0x{block.Header.GasLimit:x}",
                    transactions = includeTransactions 
                        ? block.Transactions.ToArray()
                        : block.Transactions.Select(tx => tx.Hash.ToString()).ToArray(),
                    stablecoinSettlements = block.StablecoinSettlements?.Count ?? 0,
                    complianceEvents = block.ComplianceEvents?.Count ?? 0
                };
            },

            ["lks_getTransactionByHash"] = async (parameters) =>
            {
                if (parameters.Length < 1)
                    throw new ArgumentException("Missing transaction hash parameter");

                var txHash = Hash.Parse(parameters[0].GetString()!);
                var transaction = await _blockchainService.GetTransactionAsync(txHash);

                if (transaction == null)
                    return null!;

                return new
                {
                    hash = transaction.Hash.ToString(),
                    from = transaction.From.ToString(),
                    to = transaction.To?.ToString(),
                    value = $"0x{transaction.Value:x}",
                    gasLimit = $"0x{transaction.GasLimit:x}",
                    gasPrice = $"0x{transaction.GasPrice:x}",
                    nonce = $"0x{transaction.Nonce:x}",
                    input = $"0x{Convert.ToHexString(transaction.Data)}",
                    stablecoinFee = transaction.StablecoinFee?.ToString(),
                    settlementId = transaction.SettlementId?.ToString()
                };
            },

            ["lks_sendRawTransaction"] = async (parameters) =>
            {
                if (parameters.Length < 1)
                    throw new ArgumentException("Missing transaction data parameter");

                try
                {
                    var rawTxData = parameters[0].ToString();
                    if (string.IsNullOrEmpty(rawTxData))
                        throw new ArgumentException("Invalid transaction data");

                    // Decode raw transaction from hex string
                    var txBytes = Convert.FromHexString(rawTxData.StartsWith("0x") ? rawTxData[2..] : rawTxData);
                    
                    // Deserialize transaction (assuming MessagePack serialization)
                    var transaction = MessagePack.MessagePackSerializer.Deserialize<Transaction>(txBytes);
                    
                    // Validate transaction
                    var validation = transaction.Validate();
                    if (!validation.IsValid)
                    {
                        throw new InvalidOperationException($"Invalid transaction: {string.Join(", ", validation.Errors)}");
                    }
                    
                    // Submit to transaction pool
                    var submitted = await _transactionPool.AddTransactionAsync(transaction);
                    if (!submitted)
                    {
                        throw new InvalidOperationException("Failed to submit transaction to pool");
                    }
                    
                    _logger.LogInformation("Raw transaction submitted: {Hash}", transaction.Hash);
                    
                    return new
                    {
                        transactionHash = transaction.Hash.ToString(),
                        status = "pending"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing raw transaction");
                    throw new InvalidOperationException($"Failed to process raw transaction: {ex.Message}");
                }
            },

            ["lks_estimateGas"] = async (parameters) =>
            {
                if (parameters.Length < 1)
                    throw new ArgumentException("Missing transaction object parameter");

                // This would estimate gas for the transaction
                // For now, return a default estimate
                return "0x5208"; // 21000 gas for simple transfer
            },

            ["lks_gasPrice"] = async _ =>
            {
                // Return current gas price
                return "0x3b9aca00"; // 1 Gwei
            },

            // Network methods
            ["net_version"] = async _ =>
            {
                var status = await _blockchainService.GetStatusAsync();
                return status.ChainId;
            },

            ["net_peerCount"] = async _ =>
            {
                var status = await _blockchainService.GetStatusAsync();
                return $"0x{status.PeerCount:x}";
            },

            ["net_listening"] = async _ => true,

            // Web3 methods
            ["web3_clientVersion"] = async _ =>
            {
                var status = await _blockchainService.GetStatusAsync();
                return $"LksBrothers/{status.Version}";
            },

            ["web3_sha3"] = async (parameters) =>
            {
                if (parameters.Length < 1)
                    throw new ArgumentException("Missing data parameter");

                var data = Convert.FromHexString(parameters[0].GetString()!.Replace("0x", ""));
                var hash = Hash.ComputeHash(data);
                return hash.ToString();
            }
        };
    }
}

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public required string Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement[]? Params { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; set; }

    [JsonPropertyName("id")]
    public object? Id { get; set; }
}

public class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public required string Message { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }
}
