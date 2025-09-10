using Microsoft.AspNetCore.Mvc;
using LksBrothers.Explorer.Services;

namespace LksBrothers.Explorer.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AnalyticsService _analyticsService;

        public AnalyticsController(AnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        [HttpGet("metrics/{metric}")]
        public IActionResult GetMetricData(string metric, [FromQuery] string period = "24h")
        {
            var data = _analyticsService.GetMetricData(metric, period);
            return Ok(data);
        }

        [HttpGet("network")]
        public IActionResult GetNetworkAnalytics()
        {
            var analytics = _analyticsService.GetNetworkAnalytics();
            return Ok(analytics);
        }

        [HttpGet("transactions")]
        public IActionResult GetTransactionAnalytics([FromQuery] int days = 7)
        {
            var analytics = _analyticsService.GetTransactionAnalytics(days);
            return Ok(analytics);
        }

        [HttpGet("validators")]
        public IActionResult GetValidatorAnalytics()
        {
            var analytics = _analyticsService.GetValidatorAnalytics();
            return Ok(analytics);
        }

        [HttpGet("geographic")]
        public IActionResult GetGeographicDistribution()
        {
            var distribution = _analyticsService.GetGeographicDistribution();
            return Ok(distribution);
        }

        [HttpPost("metrics/{metric}")]
        public IActionResult AddMetricDataPoint(string metric, [FromBody] AddMetricRequest request)
        {
            _analyticsService.AddMetricDataPoint(metric, request.Value);
            return Ok(new { message = "Metric data point added successfully" });
        }
    }

    public class AddMetricRequest
    {
        public double Value { get; set; }
    }
}
