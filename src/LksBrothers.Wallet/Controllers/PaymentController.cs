using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LksBrothers.Wallet.Services;
using LksBrothers.Core.Authentication;

namespace LksBrothers.Wallet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentProcessorService _paymentProcessor;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(IPaymentProcessorService paymentProcessor, ILogger<PaymentController> logger)
        {
            _paymentProcessor = paymentProcessor;
            _logger = logger;
        }

        /// <summary>
        /// Process Visa/Mastercard payment to purchase LKS coins
        /// </summary>
        [HttpPost("card-purchase")]
        public async Task<ActionResult<PaymentResult>> ProcessCardPayment([FromBody] CardPurchaseRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                
                var paymentRequest = new CardPaymentRequest
                {
                    UserId = userId,
                    Email = request.Email,
                    AmountUsd = request.AmountUsd,
                    LksAmount = CalculateLksAmount(request.AmountUsd),
                    PaymentMethodId = request.PaymentMethodId,
                    Service = "lks_purchase"
                };

                var result = await _paymentProcessor.ProcessCardPaymentAsync(paymentRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing card payment");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Create IP PATENT subscription - $5,000 USD over 12 months
        /// </summary>
        [HttpPost("ip-patent-subscription")]
        public async Task<ActionResult<SubscriptionResult>> CreateIpPatentSubscription([FromBody] IpPatentSubscriptionRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                
                var subscriptionRequest = new SubscriptionRequest
                {
                    UserId = userId,
                    Email = request.Email,
                    PlanType = "ip_patent_premium",
                    TotalAmountUsd = 5000m,
                    LksAmount = 50000m, // 50K LKS coins allocated
                    PaymentMethodId = request.PaymentMethodId
                };

                var result = await _paymentProcessor.CreateSubscriptionAsync(subscriptionRequest);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating IP PATENT subscription");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get available payment methods for user
        /// </summary>
        [HttpGet("payment-methods")]
        public async Task<ActionResult<List<PaymentMethod>>> GetPaymentMethods()
        {
            try
            {
                var userId = User.GetUserId();
                var paymentMethods = await _paymentProcessor.GetUserPaymentMethodsAsync(userId);
                return Ok(paymentMethods);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment methods");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Add new Visa/Mastercard payment method
        /// </summary>
        [HttpPost("payment-methods")]
        public async Task<ActionResult<PaymentMethod>> AddPaymentMethod([FromBody] AddPaymentMethodRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                
                var cardDetails = new CardDetails
                {
                    Number = request.CardNumber,
                    ExpiryMonth = request.ExpiryMonth,
                    ExpiryYear = request.ExpiryYear,
                    Cvc = request.Cvc,
                    Email = request.Email
                };

                var paymentMethod = await _paymentProcessor.AddPaymentMethodAsync(userId, cardDetails);
                return Ok(paymentMethod);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding payment method");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get LKS coin pricing and exchange rates
        /// </summary>
        [HttpGet("pricing")]
        [AllowAnonymous]
        public ActionResult<LksPricing> GetLksPricing()
        {
            var pricing = new LksPricing
            {
                UsdToLksRate = 0.10m, // $0.10 per LKS
                LksToUsdRate = 10.0m,  // 10 LKS per $1
                MinPurchaseUsd = 10.0m,
                MaxPurchaseUsd = 10000.0m,
                TotalSupply = 1000000000m, // 1 billion LKS
                CirculatingSupply = 600000000m, // 600M in circulation
                ZeroFees = true,
                LastUpdated = DateTime.UtcNow,
                SupportedCards = new[] { "Visa", "Mastercard", "American Express" },
                IpPatentSubscription = new IpPatentPricing
                {
                    TotalCostUsd = 5000m,
                    MonthlyPaymentUsd = 416.67m,
                    DurationMonths = 12,
                    LksAllocation = 50000m,
                    Features = new[]
                    {
                        "Unlimited IP registrations",
                        "Priority blockchain recording", 
                        "Premium certificate templates",
                        "Legal document templates",
                        "24/7 support access",
                        "50K LKS coins (usable during subscription)",
                        "Zero transaction fees"
                    }
                }
            };

            return Ok(pricing);
        }

        /// <summary>
        /// Calculate LKS amount from USD
        /// </summary>
        [HttpGet("calculate/{amountUsd}")]
        [AllowAnonymous]
        public ActionResult<decimal> CalculateLksAmount(decimal amountUsd)
        {
            const decimal usdToLksRate = 10.0m; // 10 LKS per $1
            return Ok(amountUsd * usdToLksRate);
        }

        /// <summary>
        /// Get user's subscription status
        /// </summary>
        [HttpGet("subscription-status")]
        public async Task<ActionResult<SubscriptionStatus>> GetSubscriptionStatus()
        {
            try
            {
                var userId = User.GetUserId();
                // Implementation would fetch from subscription service
                var status = new SubscriptionStatus
                {
                    UserId = userId,
                    HasActiveSubscription = false,
                    PlanType = "",
                    NextPaymentDate = null,
                    PaymentsRemaining = 0,
                    LksCoinsHeld = 0,
                    CanUseLksCoins = false
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving subscription status");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    // Request/Response models
    public class CardPurchaseRequest
    {
        public string Email { get; set; } = string.Empty;
        public decimal AmountUsd { get; set; }
        public string PaymentMethodId { get; set; } = string.Empty;
    }

    public class IpPatentSubscriptionRequest
    {
        public string Email { get; set; } = string.Empty;
        public string PaymentMethodId { get; set; } = string.Empty;
    }

    public class AddPaymentMethodRequest
    {
        public string CardNumber { get; set; } = string.Empty;
        public long ExpiryMonth { get; set; }
        public long ExpiryYear { get; set; }
        public string Cvc { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class LksPricing
    {
        public decimal UsdToLksRate { get; set; }
        public decimal LksToUsdRate { get; set; }
        public decimal MinPurchaseUsd { get; set; }
        public decimal MaxPurchaseUsd { get; set; }
        public decimal TotalSupply { get; set; }
        public decimal CirculatingSupply { get; set; }
        public bool ZeroFees { get; set; }
        public DateTime LastUpdated { get; set; }
        public string[] SupportedCards { get; set; } = Array.Empty<string>();
        public IpPatentPricing IpPatentSubscription { get; set; } = new();
    }

    public class IpPatentPricing
    {
        public decimal TotalCostUsd { get; set; }
        public decimal MonthlyPaymentUsd { get; set; }
        public int DurationMonths { get; set; }
        public decimal LksAllocation { get; set; }
        public string[] Features { get; set; } = Array.Empty<string>();
    }

    public class SubscriptionStatus
    {
        public string UserId { get; set; } = string.Empty;
        public bool HasActiveSubscription { get; set; }
        public string PlanType { get; set; } = string.Empty;
        public DateTime? NextPaymentDate { get; set; }
        public int PaymentsRemaining { get; set; }
        public decimal LksCoinsHeld { get; set; }
        public bool CanUseLksCoins { get; set; }
    }
}
