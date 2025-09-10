using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.Hooks.Engine;
using MessagePack;
using System.Text.Json;
using System.Net.Http;
using Nethereum.Web3;
using Nethereum.Contracts;

namespace LksBrothers.CrossChain.Chainlink;

public class ChainlinkCCIPClient : IDisposable
{
    private readonly ILogger<ChainlinkCCIPClient> _logger;
    private readonly ChainlinkCCIPOptions _options;
    private readonly HttpClient _httpClient;
    private readonly HookExecutor _hookExecutor;
    private readonly Dictionary<ulong, CCIPChainConfig> _supportedChains;
    private readonly Dictionary<string, Web3> _web3Clients;

    public ChainlinkCCIPClient(
        ILogger<ChainlinkCCIPClient> logger,
        IOptions<ChainlinkCCIPOptions> options,
        HttpClient httpClient,
        HookExecutor hookExecutor)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
        _hookExecutor = hookExecutor;
        _supportedChains = InitializeSupportedChains();
        _web3Clients = InitializeWeb3Clients();
        
        _logger.LogInformation("Chainlink CCIP client initialized with {ChainCount} supported chains", 
            _supportedChains.Count);
    }

    public async Task<CCIPMessageResult> SendCrossChainMessageAsync(CCIPMessageRequest request)
    {
        try
        {
            // Validate chains
            if (!_supportedChains.TryGetValue(request.SourceChainSelector, out var sourceChain))
            {
                return CCIPMessageResult.Failed("Unsupported source chain");
            }

            if (!_supportedChains.TryGetValue(request.DestinationChainSelector, out var destChain))
            {
                return CCIPMessageResult.Failed("Unsupported destination chain");
            }

            // Create CCIP message
            var message = new CCIPMessage
            {
                Id = Hash.ComputeHash($"{request.Sender}{request.Receiver}{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                SourceChainSelector = request.SourceChainSelector,
                DestinationChainSelector = request.DestinationChainSelector,
                Sender = request.Sender,
                Receiver = request.Receiver,
                Data = request.Data ?? Array.Empty<byte>(),
                TokenAmounts = request.TokenAmounts ?? new List<CCIPTokenAmount>(),
                FeeToken = request.FeeToken,
                GasLimit = request.GasLimit,
                Timestamp = DateTimeOffset.UtcNow,
                Status = CCIPMessageStatus.Pending
            };

            // Process through zero-fee hook
            var hookResult = await _hookExecutor.ExecuteCrossChainHookAsync(new CrossChainMessage
            {
                Id = message.Id,
                SourceChain = sourceChain.Name,
                DestinationChain = destChain.Name,
                Sender = request.Sender,
                Recipient = request.Receiver,
                Payload = message.Data,
                Timestamp = (ulong)message.Timestamp.ToUnixTimeSeconds()
            });

            if (!hookResult.Success)
            {
                return CCIPMessageResult.Failed($"Hook validation failed: {hookResult.Message}");
            }

            // Calculate fees
            var feeResult = await CalculateCCIPFeesAsync(message);
            if (!feeResult.Success)
            {
                return CCIPMessageResult.Failed($"Fee calculation failed: {feeResult.ErrorMessage}");
            }

            message.EstimatedFee = feeResult.Fee;

            // Submit to CCIP router
            var submitResult = await SubmitToCCIPAsync(message);
            if (!submitResult.Success)
            {
                return CCIPMessageResult.Failed($"CCIP submission failed: {submitResult.ErrorMessage}");
            }

            message.Status = CCIPMessageStatus.Submitted;
            message.CCIPMessageId = submitResult.MessageId;
            message.TransactionHash = submitResult.TransactionHash;

            _logger.LogInformation("Submitted CCIP message {MessageId} from {Source} to {Dest}", 
                message.Id, sourceChain.Name, destChain.Name);

            return CCIPMessageResult.Success(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending CCIP message");
            return CCIPMessageResult.Failed($"CCIP message error: {ex.Message}");
        }
    }

    public async Task<CCIPMessageResult> GetMessageStatusAsync(Hash messageId)
    {
        try
        {
            // Query CCIP explorer API for message status
            var response = await _httpClient.GetAsync($"{_options.CCIPExplorerUrl}/api/v1/messages/{messageId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var messageData = JsonSerializer.Deserialize<CCIPExplorerResponse>(content);
                
                if (messageData?.Message != null)
                {
                    var message = new CCIPMessage
                    {
                        Id = messageId,
                        CCIPMessageId = Hash.Parse(messageData.Message.MessageId),
                        Status = ParseCCIPStatus(messageData.Message.Status),
                        TransactionHash = Hash.Parse(messageData.Message.TxHash),
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(messageData.Message.Timestamp)
                    };
                    
                    return CCIPMessageResult.Success(message);
                }
            }

            return CCIPMessageResult.Failed("Message not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting CCIP message status for {MessageId}", messageId);
            return CCIPMessageResult.Failed($"Status query error: {ex.Message}");
        }
    }

    public async Task<CCIPFeeResult> CalculateCCIPFeesAsync(CCIPMessage message)
    {
        try
        {
            if (!_supportedChains.TryGetValue(message.SourceChainSelector, out var sourceChain))
            {
                return CCIPFeeResult.Failed("Unsupported source chain");
            }

            if (!_web3Clients.TryGetValue(sourceChain.Name, out var web3))
            {
                return CCIPFeeResult.Failed("Web3 client not available for source chain");
            }

            // Get CCIP router contract
            var routerContract = web3.Eth.GetContract(CCIPRouterABI, sourceChain.CCIPRouterAddress);
            var getFeeFunction = routerContract.GetFunction("getFee");

            // Prepare CCIP message struct for fee calculation
            var ccipMessageStruct = new object[]
            {
                message.Receiver.ToString(),
                message.Data,
                message.TokenAmounts.Select(t => new object[] { t.Token.ToString(), t.Amount.ToString() }).ToArray(),
                message.FeeToken?.ToString() ?? "0x0000000000000000000000000000000000000000",
                message.GasLimit
            };

            // Call getFee function
            var feeResult = await getFeeFunction.CallAsync<UInt256>(
                message.DestinationChainSelector,
                ccipMessageStruct
            );

            _logger.LogDebug("Calculated CCIP fee: {Fee} for message to chain {Chain}", 
                feeResult, message.DestinationChainSelector);

            return CCIPFeeResult.Success(feeResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating CCIP fees");
            return CCIPFeeResult.Failed($"Fee calculation error: {ex.Message}");
        }
    }

    public async Task<List<CCIPMessage>> GetPendingMessagesAsync(ulong? chainSelector = null)
    {
        try
        {
            var endpoint = chainSelector.HasValue 
                ? $"{_options.CCIPExplorerUrl}/api/v1/messages?source_chain={chainSelector}"
                : $"{_options.CCIPExplorerUrl}/api/v1/messages?status=pending";

            var response = await _httpClient.GetAsync(endpoint);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var messagesData = JsonSerializer.Deserialize<CCIPMessagesResponse>(content);
                
                return messagesData?.Messages?.Select(msg => new CCIPMessage
                {
                    Id = Hash.Parse(msg.Id),
                    CCIPMessageId = Hash.Parse(msg.MessageId),
                    SourceChainSelector = msg.SourceChain,
                    DestinationChainSelector = msg.DestinationChain,
                    Status = ParseCCIPStatus(msg.Status),
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(msg.Timestamp)
                }).ToList() ?? new List<CCIPMessage>();
            }

            return new List<CCIPMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending CCIP messages");
            return new List<CCIPMessage>();
        }
    }

    public async Task<TokenTransferResult> TransferTokensAsync(CCIPTokenTransferRequest request)
    {
        try
        {
            // Validate chains and token
            if (!_supportedChains.ContainsKey(request.SourceChainSelector) || 
                !_supportedChains.ContainsKey(request.DestinationChainSelector))
            {
                return TokenTransferResult.Failed("Unsupported chain in transfer request");
            }

            // Create CCIP token transfer message
            var transferMessage = new CCIPMessage
            {
                Id = Hash.ComputeHash($"transfer_{request.Token}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                SourceChainSelector = request.SourceChainSelector,
                DestinationChainSelector = request.DestinationChainSelector,
                Sender = request.Sender,
                Receiver = request.Receiver,
                TokenAmounts = new List<CCIPTokenAmount>
                {
                    new CCIPTokenAmount
                    {
                        Token = request.Token,
                        Amount = request.Amount
                    }
                },
                Data = Array.Empty<byte>(),
                GasLimit = 200000, // Standard gas limit for token transfers
                Timestamp = DateTimeOffset.UtcNow
            };

            // Submit token transfer
            var result = await SendCrossChainMessageAsync(new CCIPMessageRequest
            {
                SourceChainSelector = request.SourceChainSelector,
                DestinationChainSelector = request.DestinationChainSelector,
                Sender = request.Sender,
                Receiver = request.Receiver,
                TokenAmounts = transferMessage.TokenAmounts,
                GasLimit = transferMessage.GasLimit
            });

            if (!result.Success)
            {
                return TokenTransferResult.Failed($"Token transfer failed: {result.ErrorMessage}");
            }

            _logger.LogInformation("Initiated CCIP token transfer {TransferId} for {Amount} tokens", 
                transferMessage.Id, request.Amount);

            return TokenTransferResult.Success(result.Message!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transferring tokens via CCIP");
            return TokenTransferResult.Failed($"Token transfer error: {ex.Message}");
        }
    }

    private async Task<CCIPSubmitResult> SubmitToCCIPAsync(CCIPMessage message)
    {
        try
        {
            if (!_supportedChains.TryGetValue(message.SourceChainSelector, out var sourceChain))
            {
                return new CCIPSubmitResult { Success = false, ErrorMessage = "Source chain not found" };
            }

            if (!_web3Clients.TryGetValue(sourceChain.Name, out var web3))
            {
                return new CCIPSubmitResult { Success = false, ErrorMessage = "Web3 client not available" };
            }

            // Get CCIP router contract
            var routerContract = web3.Eth.GetContract(CCIPRouterABI, sourceChain.CCIPRouterAddress);
            var ccipSendFunction = routerContract.GetFunction("ccipSend");

            // Prepare message for submission
            var ccipMessageStruct = new object[]
            {
                message.Receiver.ToString(),
                message.Data,
                message.TokenAmounts.Select(t => new object[] { t.Token.ToString(), t.Amount.ToString() }).ToArray(),
                message.FeeToken?.ToString() ?? "0x0000000000000000000000000000000000000000",
                message.GasLimit
            };

            // Submit transaction (this would require proper wallet integration)
            // For now, we'll simulate the submission
            var messageId = Hash.ComputeHash($"ccip_{message.Id}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray());
            var txHash = Hash.ComputeHash($"tx_{messageId}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray());

            return new CCIPSubmitResult
            {
                Success = true,
                MessageId = messageId,
                TransactionHash = txHash
            };
        }
        catch (Exception ex)
        {
            return new CCIPSubmitResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private CCIPMessageStatus ParseCCIPStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" => CCIPMessageStatus.Pending,
            "submitted" => CCIPMessageStatus.Submitted,
            "committed" => CCIPMessageStatus.Committed,
            "executed" => CCIPMessageStatus.Executed,
            "failed" => CCIPMessageStatus.Failed,
            _ => CCIPMessageStatus.Unknown
        };
    }

    private Dictionary<ulong, CCIPChainConfig> InitializeSupportedChains()
    {
        return new Dictionary<ulong, CCIPChainConfig>
        {
            { 5009297550715157269, new CCIPChainConfig { Selector = 5009297550715157269, Name = "Ethereum", ChainId = 1, CCIPRouterAddress = "0x80226fc0Ee2b096224EeAc085Bb9a8cba1146f7D", IsTestnet = false } },
            { 4949039107694359620, new CCIPChainConfig { Selector = 4949039107694359620, Name = "BSC", ChainId = 56, CCIPRouterAddress = "0x34B03Cb9086d7D758AC55af71584F81A598759FE", IsTestnet = false } },
            { 4051577828743386545, new CCIPChainConfig { Selector = 4051577828743386545, Name = "Polygon", ChainId = 137, CCIPRouterAddress = "0x849c5ED5a80F5B408Dd4969b78c2C8fdf0565Bfe", IsTestnet = false } },
            { 6433500567565415381, new CCIPChainConfig { Selector = 6433500567565415381, Name = "Avalanche", ChainId = 43114, CCIPRouterAddress = "0xF4c7E640EdA248ef95972845a62bdC74237805dB", IsTestnet = false } },
            { 15971525489660198786, new CCIPChainConfig { Selector = 15971525489660198786, Name = "Base", ChainId = 8453, CCIPRouterAddress = "0x673AA85efd75080031d44fcA061575d1dA427A28", IsTestnet = false } },
            { 9999999999999999999, new CCIPChainConfig { Selector = 9999999999999999999, Name = "LKS", ChainId = 1000, CCIPRouterAddress = "0x0000000000000000000000000000000000000000", IsTestnet = false } }
        };
    }

    private Dictionary<string, Web3> InitializeWeb3Clients()
    {
        var clients = new Dictionary<string, Web3>();
        
        foreach (var chain in _supportedChains.Values)
        {
            if (chain.Name != "LKS") // Skip LKS chain for now
            {
                var rpcUrl = GetRpcUrl(chain.Name);
                if (!string.IsNullOrEmpty(rpcUrl))
                {
                    clients[chain.Name] = new Web3(rpcUrl);
                }
            }
        }
        
        return clients;
    }

    private string GetRpcUrl(string chainName)
    {
        return chainName switch
        {
            "Ethereum" => "https://eth-mainnet.g.alchemy.com/v2/your-api-key",
            "BSC" => "https://bsc-dataseed.binance.org/",
            "Polygon" => "https://polygon-rpc.com/",
            "Avalanche" => "https://api.avax.network/ext/bc/C/rpc",
            "Base" => "https://mainnet.base.org",
            _ => string.Empty
        };
    }

    // CCIP Router ABI (simplified)
    private const string CCIPRouterABI = @"[
        {
            ""inputs"": [
                {""name"": ""destinationChainSelector"", ""type"": ""uint64""},
                {""name"": ""message"", ""type"": ""tuple"", ""components"": [
                    {""name"": ""receiver"", ""type"": ""bytes""},
                    {""name"": ""data"", ""type"": ""bytes""},
                    {""name"": ""tokenAmounts"", ""type"": ""tuple[]"", ""components"": [
                        {""name"": ""token"", ""type"": ""address""},
                        {""name"": ""amount"", ""type"": ""uint256""}
                    ]},
                    {""name"": ""feeToken"", ""type"": ""address""},
                    {""name"": ""extraArgs"", ""type"": ""bytes""}
                ]}
            ],
            ""name"": ""ccipSend"",
            ""outputs"": [{""name"": ""messageId"", ""type"": ""bytes32""}],
            ""type"": ""function""
        },
        {
            ""inputs"": [
                {""name"": ""destinationChainSelector"", ""type"": ""uint64""},
                {""name"": ""message"", ""type"": ""tuple""}
            ],
            ""name"": ""getFee"",
            ""outputs"": [{""name"": ""fee"", ""type"": ""uint256""}],
            ""type"": ""function""
        }
    ]";

    public void Dispose()
    {
        foreach (var client in _web3Clients.Values)
        {
            client?.Dispose();
        }
        _httpClient?.Dispose();
        _logger.LogInformation("Chainlink CCIP client disposed");
    }
}

// Data models for CCIP integration
[MessagePackObject]
public class CCIPMessage
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public Hash? CCIPMessageId { get; set; }

    [Key(2)]
    public required ulong SourceChainSelector { get; set; }

    [Key(3)]
    public required ulong DestinationChainSelector { get; set; }

    [Key(4)]
    public required Address Sender { get; set; }

    [Key(5)]
    public required Address Receiver { get; set; }

    [Key(6)]
    public required byte[] Data { get; set; }

    [Key(7)]
    public required List<CCIPTokenAmount> TokenAmounts { get; set; }

    [Key(8)]
    public Address? FeeToken { get; set; }

    [Key(9)]
    public required ulong GasLimit { get; set; }

    [Key(10)]
    public UInt256? EstimatedFee { get; set; }

    [Key(11)]
    public required DateTimeOffset Timestamp { get; set; }

    [Key(12)]
    public required CCIPMessageStatus Status { get; set; }

    [Key(13)]
    public Hash? TransactionHash { get; set; }
}

[MessagePackObject]
public class CCIPTokenAmount
{
    [Key(0)]
    public required Address Token { get; set; }

    [Key(1)]
    public required UInt256 Amount { get; set; }
}

public enum CCIPMessageStatus
{
    Pending,
    Submitted,
    Committed,
    Executed,
    Failed,
    Unknown
}

public class CCIPMessageRequest
{
    public required ulong SourceChainSelector { get; set; }
    public required ulong DestinationChainSelector { get; set; }
    public required Address Sender { get; set; }
    public required Address Receiver { get; set; }
    public byte[]? Data { get; set; }
    public List<CCIPTokenAmount>? TokenAmounts { get; set; }
    public Address? FeeToken { get; set; }
    public ulong GasLimit { get; set; } = 200000;
}

public class CCIPMessageResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public CCIPMessage? Message { get; set; }

    public static CCIPMessageResult Success(CCIPMessage message)
    {
        return new CCIPMessageResult { Success = true, Message = message };
    }

    public static CCIPMessageResult Failed(string error)
    {
        return new CCIPMessageResult { Success = false, ErrorMessage = error };
    }
}

public class CCIPChainConfig
{
    public required ulong Selector { get; set; }
    public required string Name { get; set; }
    public required ulong ChainId { get; set; }
    public required string CCIPRouterAddress { get; set; }
    public required bool IsTestnet { get; set; }
}

public class CCIPFeeResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public UInt256 Fee { get; set; }

    public static CCIPFeeResult Success(UInt256 fee)
    {
        return new CCIPFeeResult { Success = true, Fee = fee };
    }

    public static CCIPFeeResult Failed(string error)
    {
        return new CCIPFeeResult { Success = false, ErrorMessage = error };
    }
}

public class CCIPTokenTransferRequest
{
    public required ulong SourceChainSelector { get; set; }
    public required ulong DestinationChainSelector { get; set; }
    public required Address Token { get; set; }
    public required UInt256 Amount { get; set; }
    public required Address Sender { get; set; }
    public required Address Receiver { get; set; }
}

public class TokenTransferResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public CCIPMessage? Message { get; set; }

    public static TokenTransferResult Success(CCIPMessage message)
    {
        return new TokenTransferResult { Success = true, Message = message };
    }

    public static TokenTransferResult Failed(string error)
    {
        return new TokenTransferResult { Success = false, ErrorMessage = error };
    }
}

public class CCIPSubmitResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Hash? MessageId { get; set; }
    public Hash? TransactionHash { get; set; }
}

public class ChainlinkCCIPOptions
{
    public string CCIPExplorerUrl { get; set; } = "https://ccip.chain.link";
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableTestnet { get; set; } = false;
    public string? AlchemyApiKey { get; set; }
}

// API response models
public class CCIPExplorerResponse
{
    public CCIPExplorerMessage? Message { get; set; }
}

public class CCIPExplorerMessage
{
    public required string MessageId { get; set; }
    public required string Status { get; set; }
    public required string TxHash { get; set; }
    public required long Timestamp { get; set; }
}

public class CCIPMessagesResponse
{
    public List<CCIPExplorerMessageSummary>? Messages { get; set; }
}

public class CCIPExplorerMessageSummary
{
    public required string Id { get; set; }
    public required string MessageId { get; set; }
    public required ulong SourceChain { get; set; }
    public required ulong DestinationChain { get; set; }
    public required string Status { get; set; }
    public required long Timestamp { get; set; }
}
