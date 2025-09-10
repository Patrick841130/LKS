using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LksBrothers.IpPatent.Services
{
    public interface IMultiMainnetService
    {
        Task<List<MainnetUploadResult>> UploadToAllMainnetsAsync(MainnetUploadRequest request);
        Task<MainnetUploadResult> UploadToSpecificMainnetAsync(string mainnetId, MainnetUploadRequest request);
        Task<List<MainnetInfo>> GetAvailableMainnetsAsync();
        Task<bool> VerifyMainnetUploadAsync(string mainnetId, string transactionHash);
    }

    public class MultiMainnetService : IMultiMainnetService
    {
        private readonly ILogger<MultiMainnetService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        // LKS BROTHERS IP PATENT Authority and Rights Blockchain Web3 Mainnet Configuration
        private readonly List<MainnetInfo> _targetMainnets = new()
        {
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-1",
                Name = "LKS BROTHERS IP PATENT Authority Primary",
                RpcUrl = "https://rpc1.lks-ip-patent.network",
                ChainId = "lks-ip-1",
                Authority = "LKS BROTHERS IP PATENT Authority",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-2",
                Name = "LKS BROTHERS IP PATENT Rights Secondary",
                RpcUrl = "https://rpc2.lks-ip-patent.network",
                ChainId = "lks-ip-2",
                Authority = "LKS BROTHERS IP PATENT Rights",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-3",
                Name = "LKS BROTHERS Blockchain Web3 Mainnet",
                RpcUrl = "https://rpc3.lks-ip-patent.network",
                ChainId = "lks-ip-3",
                Authority = "LKS BROTHERS Blockchain Web3",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-4",
                Name = "LKS IP PATENT Global Registry",
                RpcUrl = "https://rpc4.lks-ip-patent.network",
                ChainId = "lks-ip-4",
                Authority = "LKS IP PATENT Global",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-5",
                Name = "LKS BROTHERS Authority Mainnet",
                RpcUrl = "https://rpc5.lks-ip-patent.network",
                ChainId = "lks-ip-5",
                Authority = "LKS BROTHERS Authority",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-6",
                Name = "LKS Rights Blockchain Network",
                RpcUrl = "https://rpc6.lks-ip-patent.network",
                ChainId = "lks-ip-6",
                Authority = "LKS Rights Blockchain",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-7",
                Name = "LKS Web3 IP Protection Network",
                RpcUrl = "https://rpc7.lks-ip-patent.network",
                ChainId = "lks-ip-7",
                Authority = "LKS Web3 IP Protection",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-8",
                Name = "LKS PATENT Authority Mainnet",
                RpcUrl = "https://rpc8.lks-ip-patent.network",
                ChainId = "lks-ip-8",
                Authority = "LKS PATENT Authority",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-9",
                Name = "LKS BROTHERS Rights Registry",
                RpcUrl = "https://rpc9.lks-ip-patent.network",
                ChainId = "lks-ip-9",
                Authority = "LKS BROTHERS Rights Registry",
                Status = MainnetStatus.Active
            },
            new MainnetInfo
            {
                Id = "lks-ip-patent-mainnet-10",
                Name = "LKS Global IP Blockchain Network",
                RpcUrl = "https://rpc10.lks-ip-patent.network",
                ChainId = "lks-ip-10",
                Authority = "LKS Global IP Blockchain",
                Status = MainnetStatus.Active
            }
        };

        public MultiMainnetService(
            ILogger<MultiMainnetService> logger,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<List<MainnetUploadResult>> UploadToAllMainnetsAsync(MainnetUploadRequest request)
        {
            try
            {
                _logger.LogInformation($"Starting upload to all 10 mainnets for submission: {request.SubmissionId}");

                var results = new List<MainnetUploadResult>();
                var uploadTasks = new List<Task<MainnetUploadResult>>();

                // Create upload tasks for all mainnets
                foreach (var mainnet in _targetMainnets.Where(m => m.Status == MainnetStatus.Active))
                {
                    uploadTasks.Add(UploadToSpecificMainnetAsync(mainnet.Id, request));
                }

                // Execute all uploads in parallel
                var uploadResults = await Task.WhenAll(uploadTasks);
                results.AddRange(uploadResults);

                var successCount = results.Count(r => r.Success);
                _logger.LogInformation($"Multi-mainnet upload completed: {successCount}/{results.Count} successful for submission: {request.SubmissionId}");

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during multi-mainnet upload for submission: {request.SubmissionId}");
                throw;
            }
        }

        public async Task<MainnetUploadResult> UploadToSpecificMainnetAsync(string mainnetId, MainnetUploadRequest request)
        {
            var result = new MainnetUploadResult
            {
                MainnetId = mainnetId,
                SubmissionId = request.SubmissionId,
                StartTime = DateTime.UtcNow
            };

            try
            {
                var mainnet = _targetMainnets.FirstOrDefault(m => m.Id == mainnetId);
                if (mainnet == null)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Mainnet not found: {mainnetId}";
                    return result;
                }

                _logger.LogInformation($"Uploading to mainnet: {mainnet.Name} for submission: {request.SubmissionId}");

                // Create blockchain transaction for IP PATENT registration
                var blockchainTx = new IpPatentBlockchainTransaction
                {
                    SubmissionId = request.SubmissionId,
                    Title = request.Title,
                    Description = request.Description,
                    FileHash = request.FileHash,
                    SubmissionType = request.SubmissionType,
                    UserId = request.UserId,
                    ApprovalDate = request.ApprovalDate,
                    ReviewerId = request.ReviewerId,
                    Authority = mainnet.Authority,
                    ChainId = mainnet.ChainId,
                    Timestamp = DateTime.UtcNow,
                    GasFee = 0, // Zero fees on LKS Network
                    Metadata = new Dictionary<string, object>
                    {
                        ["mainnet_name"] = mainnet.Name,
                        ["authority"] = mainnet.Authority,
                        ["upload_source"] = "LKS_IP_PATENT_REVIEW_SYSTEM",
                        ["review_completed"] = true
                    }
                };

                // Submit transaction to mainnet
                var response = await _httpClient.PostAsJsonAsync(
                    $"{mainnet.RpcUrl}/api/ip-patent/register",
                    blockchainTx
                );

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var txResult = JsonSerializer.Deserialize<BlockchainTransactionResult>(responseContent);

                    result.Success = true;
                    result.TransactionHash = txResult?.TransactionHash ?? "";
                    result.BlockNumber = txResult?.BlockNumber ?? 0;
                    result.MainnetName = mainnet.Name;
                    result.Authority = mainnet.Authority;
                    result.CompletedTime = DateTime.UtcNow;
                    result.GasUsed = 0; // Zero fees
                    
                    _logger.LogInformation($"Successfully uploaded to {mainnet.Name}: {result.TransactionHash}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    result.Success = false;
                    result.ErrorMessage = $"Upload failed to {mainnet.Name}: {errorContent}";
                    _logger.LogError($"Failed to upload to {mainnet.Name}: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.CompletedTime = DateTime.UtcNow;
                _logger.LogError(ex, $"Exception during upload to mainnet: {mainnetId}");
            }

            return result;
        }

        public async Task<List<MainnetInfo>> GetAvailableMainnetsAsync()
        {
            try
            {
                // Check health of all mainnets
                var healthCheckTasks = _targetMainnets.Select(async mainnet =>
                {
                    try
                    {
                        var response = await _httpClient.GetAsync($"{mainnet.RpcUrl}/health");
                        mainnet.Status = response.IsSuccessStatusCode ? MainnetStatus.Active : MainnetStatus.Inactive;
                        mainnet.LastHealthCheck = DateTime.UtcNow;
                    }
                    catch
                    {
                        mainnet.Status = MainnetStatus.Inactive;
                        mainnet.LastHealthCheck = DateTime.UtcNow;
                    }
                    return mainnet;
                });

                var healthResults = await Task.WhenAll(healthCheckTasks);
                return healthResults.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking mainnet availability");
                return _targetMainnets;
            }
        }

        public async Task<bool> VerifyMainnetUploadAsync(string mainnetId, string transactionHash)
        {
            try
            {
                var mainnet = _targetMainnets.FirstOrDefault(m => m.Id == mainnetId);
                if (mainnet == null) return false;

                var response = await _httpClient.GetAsync($"{mainnet.RpcUrl}/api/transaction/{transactionHash}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying upload on mainnet: {mainnetId}");
                return false;
            }
        }
    }

    // Supporting models
    public class MainnetUploadRequest
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public string SubmissionType { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public string ReviewerId { get; set; } = string.Empty;
    }

    public class MainnetUploadResult
    {
        public string MainnetId { get; set; } = string.Empty;
        public string MainnetName { get; set; } = string.Empty;
        public string Authority { get; set; } = string.Empty;
        public string SubmissionId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string TransactionHash { get; set; } = string.Empty;
        public long BlockNumber { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? CompletedTime { get; set; }
        public long GasUsed { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class MainnetInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RpcUrl { get; set; } = string.Empty;
        public string ChainId { get; set; } = string.Empty;
        public string Authority { get; set; } = string.Empty;
        public MainnetStatus Status { get; set; }
        public DateTime? LastHealthCheck { get; set; }
    }

    public class IpPatentBlockchainTransaction
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public string SubmissionType { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime? ApprovalDate { get; set; }
        public string ReviewerId { get; set; } = string.Empty;
        public string Authority { get; set; } = string.Empty;
        public string ChainId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public long GasFee { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class BlockchainTransactionResult
    {
        public string TransactionHash { get; set; } = string.Empty;
        public long BlockNumber { get; set; }
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public enum MainnetStatus
    {
        Active,
        Inactive,
        Maintenance,
        Deprecated
    }
}
