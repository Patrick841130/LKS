using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LksBrothers.IpPatent.Services;
using LksBrothers.IpPatent.Models;
using LksBrothers.Core.Authentication;

namespace LksBrothers.IpPatent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class IpPatentController : ControllerBase
    {
        private readonly IIpPatentService _ipPatentService;
        private readonly ILogger<IpPatentController> _logger;

        public IpPatentController(IIpPatentService ipPatentService, ILogger<IpPatentController> logger)
        {
            _ipPatentService = ipPatentService;
            _logger = logger;
        }

        /// <summary>
        /// Search patents in the database
        /// </summary>
        [HttpPost("search")]
        public async Task<ActionResult<PatentSearchResult>> SearchPatents([FromBody] PatentSearchRequest request)
        {
            try
            {
                var options = new PatentSearchOptions
                {
                    MaxResults = request.MaxResults,
                    Categories = request.Categories,
                    DateFrom = request.DateFrom,
                    DateTo = request.DateTo,
                    Countries = request.Countries,
                    IncludeExpired = request.IncludeExpired
                };

                var result = await _ipPatentService.SearchPatentsAsync(request.Query, options);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching patents");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Submit a new patent application
        /// </summary>
        [HttpPost("applications")]
        public async Task<ActionResult<PatentApplication>> SubmitPatentApplication([FromBody] PatentApplicationRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                request.UserId = userId;

                var application = await _ipPatentService.SubmitPatentApplicationAsync(request);
                return CreatedAtAction(nameof(GetPatentApplication), new { id = application.Id }, application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting patent application");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get a specific patent application
        /// </summary>
        [HttpGet("applications/{id}")]
        public async Task<ActionResult<PatentApplication>> GetPatentApplication(string id)
        {
            try
            {
                var userId = User.GetUserId();
                var application = await _ipPatentService.GetPatentApplicationAsync(id, userId);
                
                if (application == null)
                    return NotFound();
                
                return Ok(application);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving patent application");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get user's IP portfolio
        /// </summary>
        [HttpGet("portfolio")]
        public async Task<ActionResult<IpPortfolio>> GetIpPortfolio()
        {
            try
            {
                var userId = User.GetUserId();
                var portfolio = await _ipPatentService.GetIpPortfolioAsync(userId);
                return Ok(portfolio);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving IP portfolio");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Process payment for IP services using LKS COIN
        /// </summary>
        [HttpPost("payments")]
        public async Task<ActionResult<PaymentResult>> ProcessPayment([FromBody] IpServicePayment payment)
        {
            try
            {
                var userId = User.GetUserId();
                payment.UserId = userId;

                var result = await _ipPatentService.ProcessIpServicePaymentAsync(payment);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing IP service payment");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get IP service pricing in LKS COIN
        /// </summary>
        [HttpGet("pricing")]
        [AllowAnonymous]
        public ActionResult<IpServicePricing> GetServicePricing()
        {
            var pricing = new IpServicePricing
            {
                PatentSearch = 10.0m,
                PatentApplication = 500.0m,
                TrademarkRegistration = 200.0m,
                CopyrightRegistration = 50.0m,
                IpValuation = 100.0m,
                PriorArtSearch = 75.0m,
                PatentAnalysis = 150.0m,
                LicensingSupport = 300.0m,
                Currency = "LKS",
                ZeroFees = true,
                LastUpdated = DateTime.UtcNow
            };

            return Ok(pricing);
        }

        /// <summary>
        /// Submit trademark application
        /// </summary>
        [HttpPost("trademarks")]
        public async Task<ActionResult<Trademark>> SubmitTrademarkApplication([FromBody] TrademarkApplicationRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                request.UserId = userId;

                var trademark = await _ipPatentService.SubmitTrademarkApplicationAsync(request);
                return CreatedAtAction(nameof(GetTrademark), new { id = trademark.Id }, trademark);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting trademark application");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get trademark details
        /// </summary>
        [HttpGet("trademarks/{id}")]
        public async Task<ActionResult<Trademark>> GetTrademark(string id)
        {
            try
            {
                var userId = User.GetUserId();
                var trademark = await _ipPatentService.GetTrademarkAsync(id, userId);
                
                if (trademark == null)
                    return NotFound();
                
                return Ok(trademark);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving trademark");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Submit copyright registration
        /// </summary>
        [HttpPost("copyrights")]
        public async Task<ActionResult<Copyright>> SubmitCopyrightRegistration([FromBody] CopyrightRegistrationRequest request)
        {
            try
            {
                var userId = User.GetUserId();
                request.UserId = userId;

                var copyright = await _ipPatentService.SubmitCopyrightRegistrationAsync(request);
                return CreatedAtAction(nameof(GetCopyright), new { id = copyright.Id }, copyright);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting copyright registration");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get copyright details
        /// </summary>
        [HttpGet("copyrights/{id}")]
        public async Task<ActionResult<Copyright>> GetCopyright(string id)
        {
            try
            {
                var userId = User.GetUserId();
                var copyright = await _ipPatentService.GetCopyrightAsync(id, userId);
                
                if (copyright == null)
                    return NotFound();
                
                return Ok(copyright);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving copyright");
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get IP analytics and insights
        /// </summary>
        [HttpGet("analytics")]
        public async Task<ActionResult<IpAnalytics>> GetIpAnalytics()
        {
            try
            {
                var userId = User.GetUserId();
                var analytics = await _ipPatentService.GetIpAnalyticsAsync(userId);
                return Ok(analytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving IP analytics");
                return BadRequest(new { error = ex.Message });
            }
        }
    }

    // Additional request/response models
    public class PatentSearchRequest
    {
        public string Query { get; set; } = string.Empty;
        public int MaxResults { get; set; } = 100;
        public string[] Categories { get; set; } = Array.Empty<string>();
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string[] Countries { get; set; } = Array.Empty<string>();
        public bool IncludeExpired { get; set; } = false;
    }

    public class IpServicePricing
    {
        public decimal PatentSearch { get; set; }
        public decimal PatentApplication { get; set; }
        public decimal TrademarkRegistration { get; set; }
        public decimal CopyrightRegistration { get; set; }
        public decimal IpValuation { get; set; }
        public decimal PriorArtSearch { get; set; }
        public decimal PatentAnalysis { get; set; }
        public decimal LicensingSupport { get; set; }
        public string Currency { get; set; } = "LKS";
        public bool ZeroFees { get; set; } = true;
        public DateTime LastUpdated { get; set; }
    }

    public class TrademarkApplicationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public List<string> Classes { get; set; } = new();
        public string Country { get; set; } = string.Empty;
    }

    public class CopyrightRegistrationRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CopyrightType Type { get; set; }
        public DateTime CreationDate { get; set; }
        public string Author { get; set; } = string.Empty;
        public string Owner { get; set; } = string.Empty;
    }

    public class IpAnalytics
    {
        public string UserId { get; set; } = string.Empty;
        public IpPortfolioStats PortfolioStats { get; set; } = new();
        public List<IpTrend> Trends { get; set; } = new();
        public List<IpRecommendation> Recommendations { get; set; } = new();
        public decimal TotalInvestment { get; set; }
        public decimal EstimatedROI { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class IpTrend
    {
        public string Category { get; set; } = string.Empty;
        public string Trend { get; set; } = string.Empty;
        public double GrowthRate { get; set; }
        public string Period { get; set; } = string.Empty;
    }

    public class IpRecommendation
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public decimal PotentialValue { get; set; }
    }
}
