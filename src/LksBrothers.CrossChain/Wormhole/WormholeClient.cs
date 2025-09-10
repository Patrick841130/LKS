using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using LksBrothers.Hooks.Engine;
using MessagePack;
using System.Text.Json;
using System.Net.Http;

namespace LksBrothers.CrossChain.Wormhole;

public class WormholeClient : IDisposable
{
    private readonly ILogger<WormholeClient> _logger;
    private readonly WormholeOptions _options;
    private readonly HttpClient _httpClient;
    private readonly HookExecutor _hookExecutor;
    private readonly Dictionary<uint, ChainConfig> _supportedChains;

    public WormholeClient(
        ILogger<WormholeClient> logger,
        IOptions<WormholeOptions> options,
        HttpClient httpClient,
        HookExecutor hookExecutor)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClient;
        _hookExecutor = hookExecutor;
        _supportedChains = InitializeSupportedChains();
        
        _logger.LogInformation("Wormhole client initialized with {ChainCount} supported chains", 
            _supportedChains.Count);
    }

    public async Task<WormholeMessageResult> SendCrossChainMessageAsync(CrossChainTransferRequest request)
    {
        try
        {
            // Validate source and destination chains
            if (!_supportedChains.TryGetValue(request.SourceChain, out var sourceChain))
            {
                return WormholeMessageResult.Failed("Unsupported source chain");
            }

            if (!_supportedChains.TryGetValue(request.DestinationChain, out var destChain))
            {
                return WormholeMessageResult.Failed("Unsupported destination chain");
            }

            // Create Wormhole message
            var message = new WormholeMessage
            {
                Id = Hash.ComputeHash($"{request.Sender}{request.Recipient}{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                SourceChain = request.SourceChain,
                DestinationChain = request.DestinationChain,
                Sender = request.Sender,
                Recipient = request.Recipient,
                TokenAddress = request.TokenAddress,
                Amount = request.Amount,
                Payload = request.Payload ?? Array.Empty<byte>(),
                Nonce = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Timestamp = DateTimeOffset.UtcNow,
                Status = WormholeMessageStatus.Pending
            };

            // Process through zero-fee hook
            var hookResult = await _hookExecutor.ExecuteCrossChainHookAsync(new CrossChainMessage
            {
                Id = message.Id,
                SourceChain = sourceChain.Name,
                DestinationChain = destChain.Name,
                Sender = request.Sender,
                Recipient = request.Recipient,
                Payload = message.Payload,
                Timestamp = (ulong)message.Timestamp.ToUnixTimeSeconds()
            });

            if (!hookResult.Success)
            {
                return WormholeMessageResult.Failed($"Hook validation failed: {hookResult.Message}");
            }

            // Submit to Wormhole network
            var submitResult = await SubmitToWormholeAsync(message);
            if (!submitResult.Success)
            {
                return WormholeMessageResult.Failed($"Wormhole submission failed: {submitResult.ErrorMessage}");
            }

            message.Status = WormholeMessageStatus.Submitted;
            message.WormholeSequence = submitResult.Sequence;
            message.WormholeTxHash = submitResult.TransactionHash;

            _logger.LogInformation("Submitted cross-chain message {MessageId} from {Source} to {Dest}", 
                message.Id, sourceChain.Name, destChain.Name);

            return WormholeMessageResult.Success(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending cross-chain message");
            return WormholeMessageResult.Failed($"Cross-chain transfer error: {ex.Message}");
        }
    }

    public async Task<WormholeMessageResult> GetMessageStatusAsync(Hash messageId)
    {
        try
        {
            // Query Wormhole API for message status
            var response = await _httpClient.GetAsync($"{_options.WormholeApiUrl}/v1/signed_vaa/{messageId}");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var vaaResponse = JsonSerializer.Deserialize<WormholeVAAResponse>(content);
                
                if (vaaResponse?.Vaa != null)
                {
                    var message = new WormholeMessage
                    {
                        Id = messageId,
                        Status = WormholeMessageStatus.Completed,
                        VAA = Convert.FromBase64String(vaaResponse.Vaa),
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    
                    return WormholeMessageResult.Success(message);
                }
            }

            return WormholeMessageResult.Failed("Message not found or not yet processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting message status for {MessageId}", messageId);
            return WormholeMessageResult.Failed($"Status query error: {ex.Message}");
        }
    }

    public async Task<List<WormholeMessage>> GetPendingMessagesAsync(uint? chainId = null)
    {
        try
        {
            var endpoint = chainId.HasValue 
                ? $"{_options.WormholeApiUrl}/v1/observations/{chainId}"
                : $"{_options.WormholeApiUrl}/v1/observations";

            var response = await _httpClient.GetAsync(endpoint);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var observations = JsonSerializer.Deserialize<WormholeObservation[]>(content);
                
                return observations?.Select(obs => new WormholeMessage
                {
                    Id = Hash.Parse(obs.Hash),
                    SourceChain = obs.EmitterChain,
                    WormholeSequence = obs.Sequence,
                    Status = WormholeMessageStatus.Observed,
                    Timestamp = DateTimeOffset.FromUnixTimeSeconds(obs.Timestamp)
                }).ToList() ?? new List<WormholeMessage>();
            }

            return new List<WormholeMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending messages");
            return new List<WormholeMessage>();
        }
    }

    public async Task<TokenBridgeResult> BridgeTokenAsync(TokenBridgeRequest request)
    {
        try
        {
            // Validate token bridge request
            if (!_supportedChains.ContainsKey(request.SourceChain) || 
                !_supportedChains.ContainsKey(request.DestinationChain))
            {
                return TokenBridgeResult.Failed("Unsupported chain in bridge request");
            }

            // Create token bridge message
            var bridgeMessage = new TokenBridgeMessage
            {
                Id = Hash.ComputeHash($"bridge_{request.TokenAddress}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                SourceChain = request.SourceChain,
                DestinationChain = request.DestinationChain,
                TokenAddress = request.TokenAddress,
                Amount = request.Amount,
                Sender = request.Sender,
                Recipient = request.Recipient,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Submit to Wormhole token bridge
            var bridgeResult = await SubmitTokenBridgeAsync(bridgeMessage);
            if (!bridgeResult.Success)
            {
                return TokenBridgeResult.Failed($"Token bridge failed: {bridgeResult.ErrorMessage}");
            }

            _logger.LogInformation("Initiated token bridge {BridgeId} for {Amount} tokens", 
                bridgeMessage.Id, request.Amount);

            return TokenBridgeResult.Success(bridgeMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bridging token");
            return TokenBridgeResult.Failed($"Token bridge error: {ex.Message}");
        }
    }

    public async Task<AttestationResult> CreateTokenAttestationAsync(Address tokenAddress, uint sourceChain)
    {
        try
        {
            if (!_supportedChains.TryGetValue(sourceChain, out var chain))
            {
                return AttestationResult.Failed("Unsupported source chain");
            }

            // Create token attestation for cross-chain recognition
            var attestation = new TokenAttestation
            {
                Id = Hash.ComputeHash($"attest_{tokenAddress}_{sourceChain}_{DateTimeOffset.UtcNow.Ticks}"u8.ToArray()),
                TokenAddress = tokenAddress,
                SourceChain = sourceChain,
                TokenName = "LKS COIN",
                TokenSymbol = "LKS",
                Decimals = 18,
                Timestamp = DateTimeOffset.UtcNow
            };

            // Submit attestation to Wormhole
            var submitResult = await SubmitAttestationAsync(attestation);
            if (!submitResult.Success)
            {
                return AttestationResult.Failed($"Attestation submission failed: {submitResult.ErrorMessage}");
            }

            _logger.LogInformation("Created token attestation {AttestationId} for {Token} on chain {Chain}", 
                attestation.Id, tokenAddress, sourceChain);

            return AttestationResult.Success(attestation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating token attestation");
            return AttestationResult.Failed($"Attestation error: {ex.Message}");
        }
    }

    private async Task<WormholeSubmitResult> SubmitToWormholeAsync(WormholeMessage message)
    {
        try
        {
            // Prepare Wormhole payload
            var payload = new WormholePayload
            {
                TargetChain = message.DestinationChain,
                Sender = message.Sender.ToString(),
                Recipient = message.Recipient.ToString(),
                Amount = message.Amount.ToString(),
                TokenAddress = message.TokenAddress?.ToString(),
                Payload = Convert.ToBase64String(message.Payload)
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_options.WormholeApiUrl}/v1/submit", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<WormholeSubmitResponse>(responseContent);
                
                return new WormholeSubmitResult
                {
                    Success = true,
                    Sequence = result?.Sequence ?? 0,
                    TransactionHash = Hash.Parse(result?.TxHash ?? "0x0")
                };
            }

            return new WormholeSubmitResult
            {
                Success = false,
                ErrorMessage = $"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}"
            };
        }
        catch (Exception ex)
        {
            return new WormholeSubmitResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<WormholeSubmitResult> SubmitTokenBridgeAsync(TokenBridgeMessage message)
    {
        // Similar to SubmitToWormholeAsync but for token bridge endpoint
        try
        {
            var payload = new TokenBridgePayload
            {
                TargetChain = message.DestinationChain,
                TokenAddress = message.TokenAddress.ToString(),
                Amount = message.Amount.ToString(),
                Recipient = message.Recipient.ToString()
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_options.WormholeApiUrl}/v1/token_bridge", content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<WormholeSubmitResponse>(responseContent);
                
                return new WormholeSubmitResult
                {
                    Success = true,
                    Sequence = result?.Sequence ?? 0,
                    TransactionHash = Hash.Parse(result?.TxHash ?? "0x0")
                };
            }

            return new WormholeSubmitResult { Success = false, ErrorMessage = "Token bridge submission failed" };
        }
        catch (Exception ex)
        {
            return new WormholeSubmitResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<WormholeSubmitResult> SubmitAttestationAsync(TokenAttestation attestation)
    {
        // Submit token attestation to Wormhole
        try
        {
            var payload = new AttestationPayload
            {
                TokenAddress = attestation.TokenAddress.ToString(),
                SourceChain = attestation.SourceChain,
                TokenName = attestation.TokenName,
                TokenSymbol = attestation.TokenSymbol,
                Decimals = attestation.Decimals
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_options.WormholeApiUrl}/v1/attest", content);
            
            if (response.IsSuccessStatusCode)
            {
                return new WormholeSubmitResult { Success = true };
            }

            return new WormholeSubmitResult { Success = false, ErrorMessage = "Attestation submission failed" };
        }
        catch (Exception ex)
        {
            return new WormholeSubmitResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private Dictionary<uint, ChainConfig> InitializeSupportedChains()
    {
        return new Dictionary<uint, ChainConfig>
        {
            { 1, new ChainConfig { Id = 1, Name = "Ethereum", RpcUrl = "https://eth-mainnet.g.alchemy.com/v2/", IsTestnet = false } },
            { 2, new ChainConfig { Id = 2, Name = "BSC", RpcUrl = "https://bsc-dataseed.binance.org/", IsTestnet = false } },
            { 3, new ChainConfig { Id = 3, Name = "Polygon", RpcUrl = "https://polygon-rpc.com/", IsTestnet = false } },
            { 4, new ChainConfig { Id = 4, Name = "Avalanche", RpcUrl = "https://api.avax.network/ext/bc/C/rpc", IsTestnet = false } },
            { 5, new ChainConfig { Id = 5, Name = "Solana", RpcUrl = "https://api.mainnet-beta.solana.com", IsTestnet = false } },
            { 6, new ChainConfig { Id = 6, Name = "Terra", RpcUrl = "https://lcd.terra.dev", IsTestnet = false } },
            { 1000, new ChainConfig { Id = 1000, Name = "LKS", RpcUrl = "https://rpc.lkscoin.io", IsTestnet = false } }
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _logger.LogInformation("Wormhole client disposed");
    }
}

// Data models for Wormhole integration
[MessagePackObject]
public class WormholeMessage
{
    [Key(0)]
    public required Hash Id { get; set; }

    [Key(1)]
    public required uint SourceChain { get; set; }

    [Key(2)]
    public required uint DestinationChain { get; set; }

    [Key(3)]
    public required Address Sender { get; set; }

    [Key(4)]
    public required Address Recipient { get; set; }

    [Key(5)]
    public Address? TokenAddress { get; set; }

    [Key(6)]
    public required UInt256 Amount { get; set; }

    [Key(7)]
    public required byte[] Payload { get; set; }

    [Key(8)]
    public required uint Nonce { get; set; }

    [Key(9)]
    public required DateTimeOffset Timestamp { get; set; }

    [Key(10)]
    public required WormholeMessageStatus Status { get; set; }

    [Key(11)]
    public ulong? WormholeSequence { get; set; }

    [Key(12)]
    public Hash? WormholeTxHash { get; set; }

    [Key(13)]
    public byte[]? VAA { get; set; }
}

public enum WormholeMessageStatus
{
    Pending,
    Submitted,
    Observed,
    Signed,
    Completed,
    Failed
}

public class CrossChainTransferRequest
{
    public required uint SourceChain { get; set; }
    public required uint DestinationChain { get; set; }
    public required Address Sender { get; set; }
    public required Address Recipient { get; set; }
    public Address? TokenAddress { get; set; }
    public required UInt256 Amount { get; set; }
    public byte[]? Payload { get; set; }
}

public class WormholeMessageResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public WormholeMessage? Message { get; set; }

    public static WormholeMessageResult Success(WormholeMessage message)
    {
        return new WormholeMessageResult { Success = true, Message = message };
    }

    public static WormholeMessageResult Failed(string error)
    {
        return new WormholeMessageResult { Success = false, ErrorMessage = error };
    }
}

public class ChainConfig
{
    public required uint Id { get; set; }
    public required string Name { get; set; }
    public required string RpcUrl { get; set; }
    public required bool IsTestnet { get; set; }
}

public class WormholeOptions
{
    public string WormholeApiUrl { get; set; } = "https://api.wormholescan.io";
    public string GuardianRpcUrl { get; set; } = "https://wormhole-v2-mainnet-api.certus.one";
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableTestnet { get; set; } = false;
}

// Additional supporting classes for API responses
public class WormholeVAAResponse
{
    public string? Vaa { get; set; }
}

public class WormholeObservation
{
    public required string Hash { get; set; }
    public required uint EmitterChain { get; set; }
    public required ulong Sequence { get; set; }
    public required long Timestamp { get; set; }
}

public class WormholePayload
{
    public required uint TargetChain { get; set; }
    public required string Sender { get; set; }
    public required string Recipient { get; set; }
    public required string Amount { get; set; }
    public string? TokenAddress { get; set; }
    public required string Payload { get; set; }
}

public class WormholeSubmitResponse
{
    public ulong Sequence { get; set; }
    public string? TxHash { get; set; }
}

public class WormholeSubmitResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ulong Sequence { get; set; }
    public Hash? TransactionHash { get; set; }
}

public class TokenBridgeRequest
{
    public required uint SourceChain { get; set; }
    public required uint DestinationChain { get; set; }
    public required Address TokenAddress { get; set; }
    public required UInt256 Amount { get; set; }
    public required Address Sender { get; set; }
    public required Address Recipient { get; set; }
}

public class TokenBridgeMessage
{
    public required Hash Id { get; set; }
    public required uint SourceChain { get; set; }
    public required uint DestinationChain { get; set; }
    public required Address TokenAddress { get; set; }
    public required UInt256 Amount { get; set; }
    public required Address Sender { get; set; }
    public required Address Recipient { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
}

public class TokenBridgeResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TokenBridgeMessage? Message { get; set; }

    public static TokenBridgeResult Success(TokenBridgeMessage message)
    {
        return new TokenBridgeResult { Success = true, Message = message };
    }

    public static TokenBridgeResult Failed(string error)
    {
        return new TokenBridgeResult { Success = false, ErrorMessage = error };
    }
}

public class TokenAttestation
{
    public required Hash Id { get; set; }
    public required Address TokenAddress { get; set; }
    public required uint SourceChain { get; set; }
    public required string TokenName { get; set; }
    public required string TokenSymbol { get; set; }
    public required byte Decimals { get; set; }
    public required DateTimeOffset Timestamp { get; set; }
}

public class AttestationResult
{
    public required bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TokenAttestation? Attestation { get; set; }

    public static AttestationResult Success(TokenAttestation attestation)
    {
        return new AttestationResult { Success = true, Attestation = attestation };
    }

    public static AttestationResult Failed(string error)
    {
        return new AttestationResult { Success = false, ErrorMessage = error };
    }
}

public class TokenBridgePayload
{
    public required uint TargetChain { get; set; }
    public required string TokenAddress { get; set; }
    public required string Amount { get; set; }
    public required string Recipient { get; set; }
}

public class AttestationPayload
{
    public required string TokenAddress { get; set; }
    public required uint SourceChain { get; set; }
    public required string TokenName { get; set; }
    public required string TokenSymbol { get; set; }
    public required byte Decimals { get; set; }
}
