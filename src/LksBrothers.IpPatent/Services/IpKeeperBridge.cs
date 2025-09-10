using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Diagnostics;

namespace LksBrothers.IpPatent.Services
{
    /// <summary>
    /// Bridge service to integrate the uploaded IP PATENT application with LKS Network
    /// </summary>
    public interface IIpPatentBridge
    {
        Task<bool> StartIpPatentServiceAsync();
        Task<IpPatentRegistration> RegisterIpWithLksPaymentAsync(IpRegistrationRequest request);
        Task<IpPatentPortfolio> GetUserPortfolioAsync(string userId);
        Task<bool> VerifyIpRegistrationAsync(string fileHash);
        Task<byte[]> GenerateCertificateAsync(string registrationId);
    }

    public class IpPatentBridge : IIpPatentBridge
    {
        private readonly ILogger<IpPatentBridge> _logger;
        private readonly IPaymentService _paymentService;
        private readonly HttpClient _httpClient;
        private readonly string _ipPatentBaseUrl;
        private Process? _ipPatentProcess;

        public IpPatentBridge(
            ILogger<IpPatentBridge> logger,
            IPaymentService paymentService,
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _logger = logger;
            _paymentService = paymentService;
            _httpClient = httpClient;
            _ipPatentBaseUrl = configuration.GetValue<string>("IpPatent:BaseUrl") ?? "http://localhost:3001";
        }

        public async Task<bool> StartIpPatentServiceAsync()
        {
            try
            {
                _logger.LogInformation("Starting IP PATENT service integration");

                var ipPatentPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "..", "assets", "IP Patent"
                );

                if (!Directory.Exists(ipPatentPath))
                {
                    _logger.LogError($"IP PATENT path not found: {ipPatentPath}");
                    return false;
                }

                // Check if IP PATENT is already running
                var healthCheck = await CheckIpPatentHealthAsync();
                if (healthCheck)
                {
                    _logger.LogInformation("IP PATENT service is already running");
                    return true;
                }

                // Start IP PATENT service
                var startInfo = new ProcessStartInfo
                {
                    FileName = "npm",
                    Arguments = "run dev",
                    WorkingDirectory = ipPatentPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _ipPatentProcess = Process.Start(startInfo);
                
                if (_ipPatentProcess == null)
                {
                    _logger.LogError("Failed to start IP PATENT process");
                    return false;
                }

                // Wait for service to be ready
                await Task.Delay(5000);
                
                var isReady = await CheckIpPatentHealthAsync();
                if (isReady)
                {
                    _logger.LogInformation("IP PATENT service started successfully");
                    return true;
                }

                _logger.LogWarning("IP PATENT service started but health check failed");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting IP PATENT service");
                return false;
            }
        }

        public async Task<IpPatentRegistration> RegisterIpWithLksPaymentAsync(IpRegistrationRequest request)
        {
            try
            {
                _logger.LogInformation($"Processing IP registration with LKS payment for user: {request.UserId}");

                // Process LKS COIN payment first (zero fees)
                var paymentResult = await _paymentService.ProcessLksCoinPaymentAsync(new PaymentRequest
                {
                    FromUserId = request.UserId,
                    ToAddress = "lks1ippatent_service_address",
                    Amount = GetRegistrationFee(request.FileType),
                    Currency = "LKS",
                    Description = $"IP Registration: {request.Title}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["service"] = "ip-patent",
                        ["file_hash"] = request.FileHash,
                        ["title"] = request.Title
                    }
                });

                if (!paymentResult.Success)
                {
                    throw new InvalidOperationException($"Payment failed: {paymentResult.ErrorMessage}");
                }

                // Forward registration to IP PATENT service
                var ipPatentRequest = new
                {
                    userId = request.UserId,
                    title = request.Title,
                    description = request.Description,
                    category = request.Category,
                    fileHash = request.FileHash,
                    fileName = request.FileName,
                    fileType = request.FileType,
                    paymentTransactionId = paymentResult.TransactionId,
                    lksNetworkIntegration = true
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{_ipPatentBaseUrl}/api/register",
                    ipPatentRequest
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"IP PATENT registration failed: {errorContent}");
                }

                var registrationResponse = await response.Content.ReadFromJsonAsync<IpPatentRegistration>();
                
                if (registrationResponse != null)
                {
                    registrationResponse.LksPaymentId = paymentResult.TransactionId;
                    registrationResponse.ZeroFees = true;
                }

                _logger.LogInformation($"IP registration completed successfully: {registrationResponse?.Id}");
                return registrationResponse ?? new IpPatentRegistration();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing IP registration with LKS payment");
                throw;
            }
        }

        public async Task<IpPatentPortfolio> GetUserPortfolioAsync(string userId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ipPatentBaseUrl}/api/portfolio/{userId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Failed to retrieve portfolio for user: {userId}");
                    return new IpPatentPortfolio { UserId = userId };
                }

                var portfolio = await response.Content.ReadFromJsonAsync<IpPatentPortfolio>();
                return portfolio ?? new IpPatentPortfolio { UserId = userId };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving portfolio for user: {userId}");
                return new IpPatentPortfolio { UserId = userId };
            }
        }

        public async Task<bool> VerifyIpRegistrationAsync(string fileHash)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ipPatentBaseUrl}/api/verify/{fileHash}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verifying IP registration: {fileHash}");
                return false;
            }
        }

        public async Task<byte[]> GenerateCertificateAsync(string registrationId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ipPatentBaseUrl}/api/certificate/{registrationId}");
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Failed to generate certificate for: {registrationId}");
                }

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating certificate: {registrationId}");
                throw;
            }
        }

        private async Task<bool> CheckIpPatentHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ipPatentBaseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private decimal GetRegistrationFee(string fileType)
        {
            return fileType.ToLower() switch
            {
                "pdf" => 25.0m,
                "jpg" => 15.0m,
                "jpeg" => 15.0m,
                "png" => 15.0m,
                _ => 20.0m
            };
        }

        public void Dispose()
        {
            try
            {
                _ipPatentProcess?.Kill();
                _ipPatentProcess?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing IP PATENT process");
            }
        }
    }

    // Models for IP PATENT integration
    public class IpRegistrationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
    }

    public class IpPatentRegistration
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public DateTime RegistrationDate { get; set; }
        public string BlockchainTxHash { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LksPaymentId { get; set; } = string.Empty;
        public bool ZeroFees { get; set; }
    }

    public class IpPatentPortfolio
    {
        public string UserId { get; set; } = string.Empty;
        public List<IpPatentRegistration> Registrations { get; set; } = new();
        public int TotalRegistrations { get; set; }
        public decimal TotalValue { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
